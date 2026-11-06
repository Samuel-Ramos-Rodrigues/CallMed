@file:OptIn(androidx.compose.foundation.layout.ExperimentalLayoutApi::class)
package com.callmed.app

import android.app.DatePickerDialog
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.selection.toggleable
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.semantics.*
import androidx.compose.ui.text.input.*
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.ui.unit.dp
import java.time.LocalDate
import java.time.ZoneId
import java.util.Calendar

@Composable internal fun AccountScreen(s: UiState, action: (AppAction) -> Unit) {
    val p = s.profile!!
    Page {
        PageTitle("SEU PERFIL", "Minha conta", "Dados, segurança e preferências em um lugar.")
        Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(14.dp)) { Avatar(p, s.photo); Column(Modifier.weight(1f)) { SectionTitle(p.nome); Muted("Conta de paciente") } }
        MenuRow(Icons.Outlined.PersonOutline, "Meus dados", "Informações pessoais e de contato", !s.busy) { action(AppAction.Open(Screen.PROFILE)) }
        MenuRow(Icons.Outlined.Shield, "Segurança e acessos", "Senha e dispositivos conectados", !s.busy) { action(AppAction.Open(Screen.SECURITY)) }
        MenuRow(Icons.Outlined.AccessibilityNew, "Acessibilidade", "Tamanho do texto, contraste e espaço") { action(AppAction.Open(Screen.READING)) }
        MenuRow(Icons.Outlined.NotificationsNone, "Avisos da clínica", "Canais e preferências de contato", !s.busy) { action(AppAction.Open(Screen.NOTIFICATIONS)) }
        MenuRow(Icons.Outlined.FolderOpen, "Meus exames", "Histórico aprovado pela clínica", !s.busy) { action(AppAction.Open(Screen.EXAMS)) }
        Secondary("Sair da conta", !s.busy) { action(AppAction.Logout) }
        Muted("CallMed ${BuildConfig.VERSION_NAME}")
    }
}
@Composable internal fun ProfileScreen(s: UiState, action: (AppAction) -> Unit) {
    val p = s.profile!!
    val picker = rememberLauncherForActivityResult(ActivityResultContracts.GetContent()) { uri -> if (uri != null) action(AppAction.UploadPhoto(uri)) }
    Page {
        Back(!s.busy) { action(AppAction.Back) }; PageTitle("MINHA CONTA", "Meus dados")
        Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(14.dp)) { Avatar(p, s.photo); TextButton(onClick = { picker.launch("image/*") }, Modifier.heightIn(min = 48.dp), enabled = !s.busy) { Text("Alterar foto") } }
        Panel {
            Detail("Nome completo", p.nome); HorizontalDivider(); Detail("E-mail", p.email.ifBlank { "Não informado" }); HorizontalDivider()
            Detail("Telefone", p.telefone.ifBlank { "Não informado" }); Detail("CPF", p.cpf); Detail("Nascimento", dateLabel(p.nascimento).ifBlank { "Não informado" })
            Detail("Convênio", if (p.temConvenio) p.nomeConvenio else "Sem convênio cadastrado")
            if (p.temConvenio) { Detail("Carteirinha", p.numeroConvenio.ifBlank { "Não informada" }); Detail("Validade", dateLabel(p.validadeConvenio).ifBlank { "Não informada" }) }
        }
        Primary("Editar dados", !s.busy) { action(AppAction.Open(Screen.EDIT_PROFILE)) }
        Secondary("Alterar e-mail", !s.busy) { action(AppAction.Open(Screen.EMAIL)) }
        Notice("A alteração de e-mail solicita sua senha atual para proteger a conta.")
    }
}
@Composable internal fun ProfileEditScreen(s: UiState, action: (AppAction) -> Unit) {
    val d = s.profileDraft ?: s.profile?.let(ProfileDraft::from) ?: return
    fun update(draft: ProfileDraft) = action(AppAction.EditProfile(draft))
    Page(Modifier.imePadding()) {
        Back(!s.busy) { action(AppAction.Back) }; PageTitle("MINHA CONTA", "Editar dados")
        OutlinedTextField(d.name, { update(d.copy(name = it.take(150))) }, Modifier.fillMaxWidth().testTag("profile-name"), label = { Text("Nome completo") }, singleLine = true, enabled = !s.busy)
        OutlinedTextField(d.phone, { update(d.copy(phone = it.take(30))) }, Modifier.fillMaxWidth(), label = { Text("Telefone") }, singleLine = true, enabled = !s.busy, keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Phone))
        DateField("Nascimento", d.birth, true, !s.busy) { update(d.copy(birth = it)) }
        Toggle("Possuo convênio", "Informe os dados que constam na sua carteirinha.", d.insured, !s.busy) { update(d.copy(insured = it)) }
        if (d.insured) {
            OutlinedTextField(d.insurer, { update(d.copy(insurer = it.take(120))) }, Modifier.fillMaxWidth(), label = { Text("Nome do convênio") }, enabled = !s.busy)
            OutlinedTextField(d.insuranceNumber, { update(d.copy(insuranceNumber = it.take(80))) }, Modifier.fillMaxWidth(), label = { Text("Número da carteirinha") }, enabled = !s.busy)
            DateField("Validade do convênio", d.expiry, false, !s.busy) { update(d.copy(expiry = it)) }
        }
        Primary("Salvar dados", !s.busy) { action(AppAction.SaveProfile) }
    }
}
@Composable internal fun DateField(label: String, value: String, birth: Boolean, enabled: Boolean, change: (String) -> Unit) {
    val context = LocalContext.current
    OutlinedTextField(value, { change(it.take(10)) }, Modifier.fillMaxWidth(), label = { Text(label) }, placeholder = { Text("dd/mm/aaaa") }, enabled = enabled, singleLine = true,
        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number), trailingIcon = {
            IconButton(onClick = {
                val today = LocalDate.now()
                val parsed = runCatching { isoDate(value)?.let(LocalDate::parse) }.getOrNull()
                val start = parsed?.takeIf { if (birth) it <= today && it >= today.minusYears(120) else it >= today } ?: if (birth) today.minusYears(25) else today
                val dialog = DatePickerDialog(context, { _, year, month, day -> change(dateLabel(LocalDate.of(year, month + 1, day).toString())) }, start.year, start.monthValue - 1, start.dayOfMonth)
                if (birth) { dialog.datePicker.maxDate = System.currentTimeMillis(); dialog.datePicker.minDate = today.minusYears(120).atStartOfDay(ZoneId.systemDefault()).toInstant().toEpochMilli() }
                else dialog.datePicker.minDate = Calendar.getInstance().apply { set(Calendar.HOUR_OF_DAY, 0); set(Calendar.MINUTE, 0); set(Calendar.SECOND, 0); set(Calendar.MILLISECOND, 0) }.timeInMillis
                dialog.show()
            }, Modifier.sizeIn(minWidth = 48.dp, minHeight = 48.dp), enabled = enabled) { Icon(Icons.Outlined.CalendarMonth, "Selecionar $label no calendário") }
        }, supportingText = { Text("Dia, mês e ano. Você também pode digitar a data.") })
}
@Composable internal fun EmailScreen(s: UiState, action: (AppAction) -> Unit) {
    var email by remember { mutableStateOf("") }; var password by remember { mutableStateOf("") }
    Page(Modifier.imePadding()) {
        Back(!s.busy) { action(AppAction.Back) }; PageTitle("PROTEÇÃO DA CONTA", "Alterar e-mail", "Confirme sua identidade antes de atualizar o contato.")
        OutlinedTextField(email, { email = it.take(256) }, Modifier.fillMaxWidth(), label = { Text("Novo e-mail") }, keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Email), singleLine = true, enabled = !s.busy)
        PasswordField("Senha atual", password, !s.busy) { password = it.take(256) }
        Notice("Os outros acessos serão encerrados. Este aplicativo continua conectado.")
        Primary("Confirmar novo e-mail", !s.busy && email.isNotBlank() && password.isNotBlank()) { action(AppAction.ChangeEmail(email, password)) }
    }
}
@Composable internal fun SecurityScreen(s: UiState, action: (AppAction) -> Unit) {
    Page {
        Back(!s.busy) { action(AppAction.Back) }; PageTitle("MINHA CONTA", "Segurança e acessos", "Confira onde sua conta está sendo utilizada.")
        if (s.sessions.isEmpty() && !s.busy) Notice("Atualize para consultar os acessos conectados.")
        s.sessions.forEach { session -> Panel {
            SectionTitle(session.device)
            Text(if (session.current) "Este acesso" else "Outro acesso conectado", color = MaterialTheme.colorScheme.primary)
            Detail("Conectado em", dateLabel(session.created))
            if (!session.current) Secondary("Encerrar este acesso", !s.busy) { action(AppAction.CloseSession(session.id)) }
        } }
        Secondary("Encerrar outros acessos", !s.busy && s.sessions.any { !it.current }) { action(AppAction.CloseSession(null)) }
        Primary("Alterar senha", !s.busy) { action(AppAction.Open(Screen.PASSWORD)) }
        Secondary("Atualizar acessos", !s.busy) { action(AppAction.Refresh) }
    }
}
@Composable internal fun PasswordScreen(s: UiState, action: (AppAction) -> Unit) {
    var current by remember { mutableStateOf("") }; var new by remember { mutableStateOf("") }; var confirmation by remember { mutableStateOf("") }
    Page(Modifier.imePadding()) {
        Back(!s.busy) { action(AppAction.Back) }; PageTitle("PROTEÇÃO DA CONTA", "Alterar senha", "Confirme a senha atual antes de salvar a nova.")
        PasswordField("Senha atual", current, !s.busy) { current = it.take(256) }
        PasswordField("Nova senha", new, !s.busy) { new = it.take(256) }
        PasswordField("Confirmar nova senha", confirmation, !s.busy) { confirmation = it.take(256) }
        Notice("Use pelo menos 8 caracteres, com maiúscula, minúscula, número e símbolo. Os outros acessos serão encerrados.")
        Primary("Salvar nova senha", !s.busy && current.isNotEmpty() && new.isNotEmpty() && confirmation.isNotEmpty()) { action(AppAction.ChangePassword(current, new, confirmation)) }
    }
}
@Composable internal fun NotificationScreen(s: UiState, action: (AppAction) -> Unit) {
    val p = s.profile!!
    var whats by remember(p.whatsApp) { mutableStateOf(p.whatsApp) }; var email by remember(p.emailAvisos) { mutableStateOf(p.emailAvisos) }; var sms by remember(p.sms) { mutableStateOf(p.sms) }; var channel by remember(p.canal) { mutableStateOf(p.canal) }
    Page {
        Back(!s.busy) { action(AppAction.Back) }; PageTitle("MINHA CONTA", "Avisos da clínica", "Escolha os canais para receber os avisos disponíveis no CallMed.")
        Panel { Toggle("WhatsApp", "", whats, !s.busy) { whats = it }; Toggle("E-mail", "", email, !s.busy) { email = it }; Toggle("SMS", "", sms, !s.busy) { sms = it } }
        SectionTitle("Canal preferido")
        listOf("WhatsApp" to "WhatsApp", "Email" to "E-mail", "SMS" to "SMS", "Telefone" to "Ligação").forEach { (value, label) -> RadioChoice(label, channel == value, !s.busy) { channel = value } }
        Primary("Salvar preferências", !s.busy) { action(AppAction.SaveNotifications(whats, email, sms, channel)) }
    }
}
@Composable internal fun ReadingScreen(s: UiState, action: (AppAction) -> Unit) {
    val r = s.reading
    Page {
        Back { action(AppAction.FinishReading) }; PageTitle("DO SEU JEITO", "Acessibilidade", "Ajuste a leitura para ficar confortável para você.")
        Panel {
            SectionTitle("Tamanho do texto")
            ChoiceGrid(listOf(1f to "Padrão", 1.25f to "Grande", 1.5f to "Maior"), 3) { (value, label) -> Choice(r.scale == value, false, label, "", "text-$label") { action(AppAction.Reading(r.copy(scale = value))) } }
            Muted("O tamanho escolhido nas configurações do Android também será respeitado.")
        }
        Panel {
            Toggle("Alto contraste", "Mais diferença entre textos e fundos.", r.contrast) { action(AppAction.Reading(r.copy(contrast = it))) }
            Toggle("Mais espaço", "Elementos mais afastados e leitura simplificada.", r.spacious) { action(AppAction.Reading(r.copy(spacious = it))) }
            Toggle("Reduzir animações", "Evita a rolagem animada da conversa.", r.reduceMotion) { action(AppAction.Reading(r.copy(reduceMotion = it))) }
        }
        Panel {
            SectionTitle("Aparência")
            listOf("dark" to "Escuro", "light" to "Claro", "system" to "Seguir o dispositivo").forEach { (value, label) -> RadioChoice(label, r.theme == value, true) { action(AppAction.Reading(r.copy(theme = value))) } }
        }
        Notice("As preferências são aplicadas neste aparelho mesmo sem internet. Com a conexão disponível, também são sincronizadas com a conta.")
        Primary("Usar estas preferências") { action(AppAction.FinishReading) }
    }
}
@Composable internal fun Toggle(label: String, description: String, checked: Boolean, enabled: Boolean = true, change: (Boolean) -> Unit) {
    Row(Modifier.fillMaxWidth().heightIn(min = 64.dp).toggleable(checked, enabled = enabled, role = Role.Switch, onValueChange = change).padding(vertical = 10.dp).semantics(mergeDescendants = true) {}, verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(12.dp)) {
        Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(4.dp)) { Text(label); if (description.isNotBlank()) Text(description, color = MaterialTheme.colorScheme.onSurfaceVariant, style = MaterialTheme.typography.bodySmall) }
        Switch(checked, onCheckedChange = null, enabled = enabled)
    }
}
