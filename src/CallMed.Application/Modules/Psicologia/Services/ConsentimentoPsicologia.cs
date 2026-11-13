using System.Security.Cryptography;
using System.Text;
namespace MKSANCrud.Services.Psicologia;
public static class ConsentimentoPsicologia {
 public const string Versao="psicologia-video-v1";
 public const string Texto="A sessão de psicologia acontece entre o paciente e o psicólogo responsável, identificado pelo nome e CRP. Autoriza-se a transmissão de imagem e voz para esta sessão. O CallMed usa WebRTC com mídia cifrada entre os participantes, podendo utilizar um retransmissor TURN, e não grava a chamada. Cada participante deve buscar ambiente privativo. Se houver interrupção ou inadequação do atendimento online, o psicólogo combinará uma alternativa ou encaminhamento presencial. Formulários e registros possuem chaves de acesso próprias. Este aceite não substitui o contrato de prestação de serviços e as orientações combinadas com o psicólogo. Em urgência, procure atendimento humano adequado.";
 public static string Hash=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Texto))).ToLowerInvariant();
}
