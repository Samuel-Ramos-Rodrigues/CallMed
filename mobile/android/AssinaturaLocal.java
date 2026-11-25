import java.io.Console;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.nio.channels.FileChannel;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.StandardOpenOption;
import java.security.DigestInputStream;
import java.security.KeyStore;
import java.security.MessageDigest;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.HexFormat;
import java.util.List;

class AssinaturaLocal {
    private static final String ALIAS = "callmed-site";
    private static final String SENHA = "CALLMED_KEYSTORE_PASSWORD";
    private static final boolean WINDOWS = System.getProperty("os.name").startsWith("Windows");

    public static void main(String[] args) {
        try {
            executar();
        } catch (Exception e) {
            System.err.println("Falha: " + e.getMessage());
            System.exit(1);
        }
    }

    private static void executar() throws Exception {
        Console console = System.console();
        if (console == null) throw new IOException("Abra ASSINAR-CALLMED.bat em uma janela de terminal.");
        Path android = Path.of("").toAbsolutePath();
        if (!Files.isRegularFile(android.resolve("settings.gradle"))) throw new IOException("Abra o assinador na raiz do projeto.");
        Path pasta = Path.of(System.getProperty("user.home"), ".callmed", "assinatura");
        Files.createDirectories(pasta);
        Path chave = pasta.resolve(ALIAS + ".p12");
        try (var canal = FileChannel.open(pasta.resolve(".lock"), StandardOpenOption.CREATE, StandardOpenOption.WRITE);
             var trava = canal.tryLock()) {
            if (trava == null) throw new IOException("Ja existe uma assinatura em andamento neste computador.");
            boolean nova = !Files.exists(chave);
            if (nova) {
                Path publicado = android.resolve("../../src/CallMed.Web/App_Data/Aplicativo/CallMed.apk").normalize();
                MessageDigest hash = MessageDigest.getInstance("SHA-256");
                try (var in = new DigestInputStream(Files.newInputStream(publicado), hash)) {
                    in.transferTo(OutputStream.nullOutputStream());
                }
                if (!HexFormat.of().formatHex(hash.digest()).equals("47a71fd059483e3d78c8c2901eb696e9b24d0d663e7b28c67d3c1dd14b7d8051")) {
                    throw new IOException("Este projeto ja usa outro APK. Restaure sua copia de " + chave + " antes de continuar.");
                }
            }
            System.out.println("Chave de assinatura: " + chave);
            System.out.println(nova ? "Crie uma senha para a nova chave e guarde-a no seu gerenciador de senhas." : "Usando a chave que ja existe neste computador.");
            char[] senha = console.readPassword(nova ? "Nova senha (6 ou mais caracteres ASCII): " : "Senha da chave: ");
            if (senha == null) throw new IOException("Operacao cancelada.");
            try {
                if (nova) {
                    if (senha.length < 6 || senha[0] == ' ' || senha[senha.length-1] == ' ')
                        throw new IOException("Use ao menos 6 caracteres, sem espaco nas pontas.");
                    for (char c : senha) if (c < 32 || c > 126) throw new IOException("Use letras sem acentos, numeros ou simbolos ASCII.");
                    char[] repetida = console.readPassword("Repita a senha: ");
                    boolean igual = Arrays.equals(senha, repetida);
                    if (repetida != null) Arrays.fill(repetida, '\0');
                    if (!igual) throw new IOException("As senhas diferem. Abra o assinador novamente.");
                    criarChave(chave, senha);
                    System.out.println("Chave criada. Faca uma copia de seguranca do arquivo .p12 fora deste computador.");
                }
                var ks = KeyStore.getInstance("PKCS12");
                try (InputStream in = Files.newInputStream(chave)) { ks.load(in, senha); }
                if (!ks.isKeyEntry(ALIAS)) throw new IOException("O arquivo nao contem o alias " + ALIAS + ".");
                String certificado = HexFormat.of().formatHex(MessageDigest.getInstance("SHA-256").digest(ks.getCertificate(ALIAS).getEncoded()));
                System.out.println("Certificado SHA-256: " + certificado);
                List<String> args = new ArrayList<>(WINDOWS ? List.of("cmd.exe", "/d", "/c", "gradlew.bat") : List.of("sh", "gradlew"));
                args.addAll(List.of("--no-daemon", "--no-configuration-cache", ":app:prepararApkSite",
                    "-PcallmedUsarPrecompilado", "-PcallmedCertificadoLocal=" + certificado));
                ProcessBuilder processo = new ProcessBuilder(args).directory(android.toFile()).inheritIO();
                var env = processo.environment();
                env.put("JAVA_HOME", System.getProperty("java.home"));
                env.put("CALLMED_KEYSTORE_FILE", chave.toString());
                env.put("CALLMED_KEY_ALIAS", ALIAS);
                env.put(SENHA, new String(senha));
                env.put("CALLMED_KEY_PASSWORD", env.get(SENHA));
                if (WINDOWS && !env.containsKey("ANDROID_HOME") && !env.containsKey("ANDROID_SDK_ROOT") && env.containsKey("LOCALAPPDATA")) {
                    Path sdk = Path.of(env.get("LOCALAPPDATA"), "Android", "Sdk");
                    if (Files.isDirectory(sdk)) env.put("ANDROID_HOME", sdk.toString());
                }
                if (processo.start().waitFor() != 0) throw new IOException("Confira a mensagem do Gradle acima. A chave fica guardada para repetir a assinatura.");
                System.out.println("Arquivos para o site: " + android.resolve("../../src/CallMed.Web/App_Data/Aplicativo").normalize());
            } finally {
                Arrays.fill(senha, '\0');
            }
        }
    }

    private static void criarChave(Path chave, char[] senha) throws Exception {
        Path tmp = Files.createTempDirectory(chave.getParent(), "geracao-");
        Path provisoria = tmp.resolve("nova.p12");
        try {
            String keytool = Path.of(System.getProperty("java.home"), "bin", WINDOWS ? "keytool.exe" : "keytool").toString();
            var cmd = new ProcessBuilder(keytool, "-genkeypair", "-storetype", "PKCS12", "-keystore", provisoria.toString(),
                "-storepass:env", SENHA, "-alias", ALIAS, "-keyalg", "RSA", "-keysize", "3072",
                "-sigalg", "SHA256withRSA", "-validity", "10000", "-dname", "CN=CallMed, O=Ouroboros Dev, C=BR").inheritIO();
            cmd.environment().put(SENHA, new String(senha));
            if (cmd.start().waitFor() != 0) throw new IOException("O keytool nao conseguiu criar a chave.");
            Files.move(provisoria, chave);
        } finally {
            Files.deleteIfExists(provisoria);
            Files.deleteIfExists(tmp);
        }
    }
}
