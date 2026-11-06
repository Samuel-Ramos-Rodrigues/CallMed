@file:OptIn(androidx.compose.foundation.layout.ExperimentalLayoutApi::class)
package com.callmed.app

import android.content.Intent
import android.graphics.BitmapFactory
import android.net.Uri
import androidx.activity.compose.BackHandler
import androidx.compose.foundation.*
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.selection.selectable
import androidx.compose.foundation.selection.selectableGroup
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material.icons.outlined.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.semantics.*
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.*
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.platform.testTag

@Composable
internal fun CallMedApp(s: UiState, onAction: (AppAction) -> Unit) {
    val snackbar = remember { SnackbarHostState() }
    var confirmation by remember { mutableStateOf<AppAction?>(null) }
    LaunchedEffect(s.info) { s.info?.let { snackbar.showSnackbar(it); onAction(AppAction.DismissInfo) } }
    BackHandler(enabled = s.screen != Screen.HOME && (s.screen == Screen.READING || !s.busy)) { onAction(AppAction.Back) }
    if (s.error != null) AlertDialog(onDismissRequest = { onAction(AppAction.DismissError) },
        title = { Text("Confira esta informação") }, text = { Text(s.error, Modifier.semantics { liveRegion = LiveRegionMode.Polite }) },
        confirmButton = { TextButton(onClick = { onAction(AppAction.DismissError) }, Modifier.heightIn(min = 48.dp)) { Text("Entendi") } })
    confirmation?.let { event ->
        val cancel = event is AppAction.AppointmentChange && event.action == "cancelar"
        val exit = event == AppAction.Logout
        val newChat = event == AppAction.NewChat
        val title = if (cancel) "Cancelar esta consulta?" else if (exit) "Sair da sua conta?" else if (newChat) "Iniciar outro atendimento?" else "Encerrar outros acessos?"
        val detail = if (cancel) { val a = (event as AppAction.AppointmentChange).appointment; "${a.medico}\n${dateLabel(a.data)} às ${a.horario}\nO horário será liberado após confirmar." }
            else if (exit) "Seu acesso neste aplicativo será encerrado." else if (newChat) "O atendimento atual será finalizado e ficará em Atendimentos anteriores. A nova conversa começará vazia." else "Os acessos selecionados precisarão entrar novamente. Este aplicativo continuará conectado."
        AlertDialog(onDismissRequest = { confirmation = null }, title = { Text(title) }, text = { Text(detail) },
            confirmButton = { TextButton(onClick = { confirmation = null; onAction(event) }, Modifier.heightIn(min = 48.dp)) { Text(if (cancel) "Sim, cancelar consulta" else if (exit) "Sair da conta" else if (newChat) "Iniciar novo" else "Sim, encerrar") } },
            dismissButton = { TextButton(onClick = { confirmation = null }, Modifier.heightIn(min = 48.dp)) { Text(if (cancel) "Manter minha consulta" else "Voltar") } })
    }
    fun dispatch(event: AppAction) {
        if (event == AppAction.Logout || event is AppAction.CloseSession || (event == AppAction.NewChat && !s.chat.encerrada && s.chat.messages.isNotEmpty()) || (event is AppAction.AppointmentChange && event.action == "cancelar")) confirmation = event
        else onAction(event)
    }
    Scaffold(containerColor = MaterialTheme.colorScheme.background, contentWindowInsets = WindowInsets(0, 0, 0, 0),
        topBar = { AppHeader(s, onAction) },
        bottomBar = { if (s.authenticated && !WindowInsets.isImeVisible) BottomNavigation(s, onAction) },
        snackbarHost = { SnackbarHost(snackbar) }
    ) { padding ->
        Box(Modifier.fillMaxSize().padding(padding).consumeWindowInsets(padding).semantics { paneTitle = screenTitle(s.screen) }) {
            val reading = s.screen == Screen.READING
            val contentScreen = if (reading) s.readingReturn else s.screen
            val contentState = if (reading) s.copy(busy = true) else s
            Box(Modifier.fillMaxSize().then(if (reading) Modifier.graphicsLayer { alpha = 0f }.clearAndSetSemantics {} else Modifier)) {
            when {
                s.restoring -> Column(Modifier.fillMaxWidth().padding(28.dp), verticalArrangement = Arrangement.spacedBy(20.dp)) {
                    PageTitle("SEU ESPAÇO", "Abrindo sua conta"); CircularProgressIndicator(); Text("Carregando seus dados…", Modifier.semantics { liveRegion = LiveRegionMode.Polite })
                }
                !s.authenticated -> LoginScreen(contentState.busy, onAction)
                s.profile == null -> Page { PageTitle("SUA CONTA", "Vamos atualizar seus dados"); Muted("Verifique a conexão para carregar suas consultas e seu perfil."); Primary("Tentar novamente", !s.busy) { onAction(AppAction.Refresh) }; Secondary("Sair da conta", !s.busy) { dispatch(AppAction.Logout) } }
                else -> when (contentScreen) {
                    Screen.HOME -> HomeScreen(contentState, onAction)
                    Screen.CONSULTAS -> ConsultasScreen(contentState, ::dispatch)
                    Screen.BOOKING -> BookingScreen(contentState, onAction)
                    Screen.BOOKED -> BookingSuccess(contentState, onAction)
                    Screen.CHAT -> ChatScreen(contentState, ::dispatch)
                    Screen.CHAT_ARCHIVE, Screen.CHAT_PREVIOUS -> PreviousChatsScreen(contentState, onAction)
                    Screen.ACCOUNT -> AccountScreen(contentState, ::dispatch)
                    Screen.PROFILE -> ProfileScreen(contentState, onAction)
                    Screen.EDIT_PROFILE -> ProfileEditScreen(contentState, onAction)
                    Screen.EMAIL -> EmailScreen(contentState, onAction)
                    Screen.SECURITY -> SecurityScreen(contentState, ::dispatch)
                    Screen.PASSWORD -> PasswordScreen(contentState, onAction)
                    Screen.NOTIFICATIONS -> NotificationScreen(contentState, onAction)
                    Screen.READING -> ReadingScreen(s, onAction)
                    Screen.EXAMS -> ExamsScreen(contentState, onAction)
                }
            }
            }
            if (reading) Surface(Modifier.fillMaxSize(), color = MaterialTheme.colorScheme.background) { ReadingScreen(s, onAction) }
        }
    }
}

@Composable private fun AppHeader(s: UiState, action: (AppAction) -> Unit) {
    Surface(color = MaterialTheme.colorScheme.background) {
        Column(Modifier.statusBarsPadding()) {
            FlowRow(Modifier.fillMaxWidth().padding(horizontal = 18.dp, vertical = 8.dp), horizontalArrangement = Arrangement.SpaceBetween, verticalArrangement = Arrangement.spacedBy(6.dp)) {
                Row(Modifier.heightIn(min = 48.dp), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    Image(painterResource(R.drawable.logo_callmed), null, Modifier.size(32.dp))
                    Text("CallMed", style = MaterialTheme.typography.titleLarge)
                }
                OutlinedButton(onClick = { action(AppAction.Open(Screen.READING)) }, Modifier.heightIn(min = 48.dp).testTag("reading"), contentPadding = PaddingValues(horizontal = 12.dp, vertical = 8.dp), shape = RoundedCornerShape(14.dp)) {
                    Icon(Icons.Outlined.AccessibilityNew, null, Modifier.size(21.dp)); Spacer(Modifier.width(7.dp)); Text("Leitura")
                }
            }
            HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
            if (s.busy) LinearProgressIndicator(Modifier.fillMaxWidth().semantics { contentDescription = "Atualizando informações" })
        }
    }
}
@Composable private fun BottomNavigation(s: UiState, action: (AppAction) -> Unit) {
    Surface(color = MaterialTheme.colorScheme.surface, tonalElevation = 1.dp) {
        Column(Modifier.navigationBarsPadding()) {
            HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
            Row(Modifier.fillMaxWidth().padding(horizontal = 6.dp, vertical = 7.dp), horizontalArrangement = Arrangement.spacedBy(3.dp)) {
                Tab.entries.forEach { tab ->
                    val selected = s.tab == tab
                    val shape = RoundedCornerShape(14.dp)
                    Column(Modifier.weight(1f).heightIn(min = 64.dp).clip(shape)
                        .background(if (selected) MaterialTheme.colorScheme.primaryContainer else MaterialTheme.colorScheme.surface)
                        .selectable(selected, enabled = !s.busy, role = Role.Tab, onClick = { action(AppAction.Navigate(tab)) })
                        .padding(horizontal = 3.dp, vertical = 8.dp).testTag("nav-${tab.name}"),
                        horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(4.dp)) {
                        Icon(when(tab) { Tab.HOME -> Icons.Outlined.Home; Tab.CONSULTAS -> Icons.Outlined.CalendarMonth; Tab.CHAT -> Icons.Outlined.ChatBubbleOutline; Tab.CONTA -> Icons.Outlined.PersonOutline }, null, Modifier.size(22.dp), tint = if (selected) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.onSurfaceVariant)
                        Text(tab.label, fontSize = 13.sp, lineHeight = 18.sp, color = if (selected) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                }
            }
        }
    }
}
internal fun screenTitle(screen: Screen) = when(screen) {
    Screen.HOME -> "Início"; Screen.CONSULTAS -> "Minhas consultas"; Screen.BOOKING -> "Agendar consulta"; Screen.BOOKED -> "Consulta registrada"
    Screen.CHAT -> "Atendimento"; Screen.ACCOUNT -> "Minha conta"; Screen.PROFILE -> "Meus dados"; Screen.EDIT_PROFILE -> "Editar dados"
    Screen.CHAT_ARCHIVE -> "Atendimentos anteriores"; Screen.CHAT_PREVIOUS -> "Conversa anterior"
    Screen.EMAIL -> "Alterar e-mail"; Screen.SECURITY -> "Segurança e acessos"; Screen.PASSWORD -> "Alterar senha"
    Screen.NOTIFICATIONS -> "Avisos da clínica"; Screen.READING -> "Acessibilidade"; Screen.EXAMS -> "Meus exames"
}
@Composable internal fun Page(modifier: Modifier = Modifier, content: @Composable ColumnScope.() -> Unit) {
    val reading = LocalReading.current
    Column(modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(horizontal = 20.dp, vertical = 22.dp).testTag("page"),
        verticalArrangement = Arrangement.spacedBy(if (reading.spacious) 26.dp else 18.dp), content = content)
}
@Composable internal fun PageTitle(eyebrow: String, title: String, subtitle: String = "") {
    Column(verticalArrangement = Arrangement.spacedBy(9.dp)) {
        Text(eyebrow.uppercase(), color = MaterialTheme.colorScheme.primary, style = MaterialTheme.typography.labelSmall)
        Text(title, style = MaterialTheme.typography.headlineLarge, modifier = Modifier.semantics { heading() })
        if (subtitle.isNotBlank()) Muted(subtitle)
    }
}
@Composable internal fun SectionTitle(text: String) { Text(text, style = MaterialTheme.typography.titleMedium, modifier = Modifier.semantics { heading() }) }
@Composable internal fun Muted(text: String) { Text(text, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant) }
@Composable internal fun Panel(featured: Boolean = false, content: @Composable ColumnScope.() -> Unit) {
    Surface(Modifier.fillMaxWidth(), shape = RoundedCornerShape(20.dp), color = if (featured) MaterialTheme.colorScheme.primaryContainer else MaterialTheme.colorScheme.surface,
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant)) {
        Column(Modifier.padding(18.dp), verticalArrangement = Arrangement.spacedBy(14.dp), content = content)
    }
}
@Composable internal fun Primary(text: String, enabled: Boolean = true, icon: ImageVector? = null, action: () -> Unit) {
    Button(onClick = action, Modifier.fillMaxWidth().heightIn(min = 50.dp), enabled = enabled, shape = RoundedCornerShape(14.dp), contentPadding = PaddingValues(horizontal = 14.dp, vertical = 12.dp)) {
        if (icon != null) { Icon(icon, null, Modifier.size(21.dp)); Spacer(Modifier.width(8.dp)) }; Text(text)
    }
}
@Composable internal fun Secondary(text: String, enabled: Boolean = true, action: () -> Unit) {
    OutlinedButton(onClick = action, Modifier.fillMaxWidth().heightIn(min = 48.dp), enabled = enabled, shape = RoundedCornerShape(14.dp), contentPadding = PaddingValues(horizontal = 14.dp, vertical = 12.dp)) { Text(text) }
}
@Composable internal fun Back(enabled: Boolean = true, action: () -> Unit) { TextButton(onClick = action, Modifier.heightIn(min = 48.dp), enabled = enabled, contentPadding = PaddingValues(horizontal = 0.dp, vertical = 8.dp)) { Icon(Icons.AutoMirrored.Outlined.ArrowBack, null); Spacer(Modifier.width(8.dp)); Text("Voltar") } }
@Composable internal fun Notice(text: String) { Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(11.dp)) { Box(Modifier.width(3.dp).heightIn(min = 32.dp).background(MaterialTheme.colorScheme.primary)); Text(text, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant) } }
@Composable internal fun MenuRow(icon: ImageVector, title: String, description: String = "", enabled: Boolean = true, action: () -> Unit) {
    Surface(onClick = action, Modifier.fillMaxWidth().heightIn(min = 64.dp), enabled = enabled, color = MaterialTheme.colorScheme.surface, shape = RoundedCornerShape(14.dp), border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant)) {
        Row(Modifier.padding(16.dp), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(12.dp)) {
            Icon(icon, null, Modifier.size(22.dp), tint = MaterialTheme.colorScheme.primary)
            Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(3.dp)) { Text(title, fontWeight = FontWeight.Medium); if (description.isNotBlank()) Text(description, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant) }
            Icon(Icons.Outlined.ChevronRight, null, Modifier.size(20.dp))
        }
    }
}
@Composable internal fun DoctorInfo(name: String, specialty: String) {
    Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(12.dp), modifier = Modifier.semantics(mergeDescendants = true) {}) {
        Box(Modifier.size(46.dp).clip(RoundedCornerShape(14.dp)).background(MaterialTheme.colorScheme.surfaceVariant), contentAlignment = Alignment.Center) { Icon(Icons.Outlined.MedicalServices, null, tint = MaterialTheme.colorScheme.primary) }
        Column(Modifier.weight(1f)) { Text(name, style = MaterialTheme.typography.titleMedium); Text(specialty, color = MaterialTheme.colorScheme.onSurfaceVariant, style = MaterialTheme.typography.bodySmall) }
    }
}
@Composable internal fun StatusLabel(status: String) {
    val negative = status in listOf("Cancelada", "Ausente")
    val positive = status in listOf("Confirmada", "Realizada")
    val background = when { negative -> MaterialTheme.colorScheme.errorContainer; positive -> MaterialTheme.colorScheme.secondaryContainer; else -> MaterialTheme.colorScheme.primaryContainer }
    val foreground = when { negative -> MaterialTheme.colorScheme.onErrorContainer; positive -> MaterialTheme.colorScheme.onSecondaryContainer; else -> MaterialTheme.colorScheme.onPrimaryContainer }
    Surface(color = background, shape = RoundedCornerShape(9.dp), modifier = Modifier.semantics(mergeDescendants = true) {}) {
        Row(Modifier.padding(horizontal = 9.dp, vertical = 6.dp), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(6.dp)) {
            Icon(when { negative -> Icons.Outlined.Cancel; positive -> Icons.Outlined.CheckCircleOutline; else -> Icons.Outlined.Schedule }, null, Modifier.size(17.dp), tint = foreground)
            Text(status, color = foreground, style = MaterialTheme.typography.labelMedium)
        }
    }
}
@Composable internal fun Avatar(p: Patient, photo: ByteArray?, size: Int = 64) {
    val bitmap = remember(photo) { photo?.let { bytes -> runCatching { val bounds = BitmapFactory.Options().apply { inJustDecodeBounds = true }; BitmapFactory.decodeByteArray(bytes, 0, bytes.size, bounds); var sample = 1; while (bounds.outWidth / sample > 512 || bounds.outHeight / sample > 512) sample *= 2; BitmapFactory.decodeByteArray(bytes, 0, bytes.size, BitmapFactory.Options().apply { inSampleSize = sample })?.asImageBitmap() }.getOrNull() } }
    Box(Modifier.size(size.dp).clip(CircleShape).background(MaterialTheme.colorScheme.primaryContainer).clearAndSetSemantics { contentDescription = "Foto do perfil" }, contentAlignment = Alignment.Center) {
        if (bitmap != null) Image(bitmap, null, Modifier.fillMaxSize(), contentScale = androidx.compose.ui.layout.ContentScale.Crop)
        else Text(p.iniciais, color = MaterialTheme.colorScheme.onPrimaryContainer, style = MaterialTheme.typography.titleLarge)
    }
}

@Composable private fun HomeScreen(s: UiState, action: (AppAction) -> Unit) {
    val p = s.profile!!
    val next = s.appointments.filter { it.futura }.minWithOrNull(compareBy<Appointment> { it.data }.thenBy { it.horario })
    Page {
        PageTitle("SEU ESPAÇO", "Olá, ${p.primeiroNome}.", "Acompanhe seu atendimento e escolha seu próximo horário.")
        Primary("Agendar consulta", !s.busy, Icons.Outlined.Add) { action(AppAction.StartBooking) }
        SectionTitle("Próxima consulta")
        if (next == null) Panel { Text("Sua próxima consulta começa aqui."); Muted("Encontre uma especialidade e escolha o melhor horário.") }
        else AppointmentCard(next, s.busy, full = false, featured = true, action = action)
        MenuRow(Icons.Outlined.FolderOpen, "Meus exames", "Registros liberados pela clínica", !s.busy) { action(AppAction.Open(Screen.EXAMS)) }
        MenuRow(Icons.Outlined.ChatBubbleOutline, "Falar com a CallMed", "Assistente e equipe no mesmo lugar", !s.busy) { action(AppAction.Navigate(Tab.CHAT)) }
    }
}
@Composable private fun ConsultasScreen(s: UiState, action: (AppAction) -> Unit) {
    var history by remember { mutableStateOf(false) }
    val items = s.appointments.filter { if (history) !it.futura else it.futura }.sortedWith(if (history) compareByDescending<Appointment> { it.data }.thenByDescending { it.horario } else compareBy<Appointment> { it.data }.thenBy { it.horario })
    Page {
        PageTitle("SUA AGENDA", "Minhas consultas", "Acompanhe horários, detalhes e alterações.")
        Primary("Agendar consulta", !s.busy, Icons.Outlined.Add) { action(AppAction.StartBooking) }
        Row(Modifier.selectableGroup(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            FilterChip(!history, { history = false }, { Text("Próximas") }, Modifier.weight(1f).heightIn(min = 48.dp))
            FilterChip(history, { history = true }, { Text("Histórico") }, Modifier.weight(1f).heightIn(min = 48.dp))
        }
        if (items.isEmpty()) Panel { Text(if (history) "Nenhuma consulta no histórico." else "Nenhuma consulta futura."); Muted(if (history) "Consultas concluídas ou canceladas aparecerão aqui." else "Use Agendar consulta para encontrar um horário.") }
        items.forEach { AppointmentCard(it, s.busy, full = true, action = action) }
        Secondary("Atualizar consultas", !s.busy) { action(AppAction.Refresh) }
    }
}
@Composable internal fun AppointmentCard(a: Appointment, busy: Boolean, full: Boolean, featured: Boolean = false, action: (AppAction) -> Unit) {
    Panel(featured) {
        StatusLabel(a.status)
        DoctorInfo(a.medico, a.especialidade)
        HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
        Row(horizontalArrangement = Arrangement.spacedBy(10.dp), verticalAlignment = Alignment.Top) { Icon(Icons.Outlined.CalendarMonth, null, tint = MaterialTheme.colorScheme.primary); Column(Modifier.weight(1f)) { Text("${dateLabel(a.data)} · ${a.horario}", fontWeight = FontWeight.Medium); Text(longDateLabel(a.data), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant) } }
        if (full) {
            Text(if (a.pagamento == "Convenio") "Convênio: ${a.convenio}" else "Atendimento particular.", style = MaterialTheme.typography.bodySmall)
            if (a.observacaoConvenio.isNotBlank()) Notice(a.observacaoConvenio)
            if (a.podeConfirmar) Primary("Confirmar consulta", !busy) { action(AppAction.AppointmentChange(a, "confirmar")) }
            if (a.podeRemarcar) Secondary("Escolher outro horário", !busy) { action(AppAction.Reschedule(a)) }
            if (a.podeCancelar) TextButton(onClick = { action(AppAction.AppointmentChange(a, "cancelar")) }, Modifier.heightIn(min = 48.dp), enabled = !busy) { Text("Cancelar consulta", color = MaterialTheme.colorScheme.error) }
        } else TextButton(onClick = { action(AppAction.Navigate(Tab.CONSULTAS)) }, Modifier.heightIn(min = 48.dp), enabled = !busy) { Text("Ver minha consulta") }
    }
}
@Composable private fun ExamsScreen(s: UiState, action: (AppAction) -> Unit) {
    Page {
        Back(!s.busy) { action(AppAction.Back) }
        PageTitle("SEUS REGISTROS", "Meus exames", "Exames aprovados pela equipe, como no histórico do site.")
        if (s.exams.isEmpty()) Panel { Text("Nenhum exame liberado."); Muted("Os registros aparecerão após a aprovação da clínica.") }
        s.exams.forEach { exam -> Panel { SectionTitle(exam.name); Detail("Realizado em", dateLabel(exam.date)); Detail("Local", exam.place.ifBlank { "Não informado" }) } }
        Secondary("Atualizar histórico", !s.busy) { action(AppAction.Refresh) }
    }
}
@Composable internal fun Detail(label: String, value: String) { Column(Modifier.fillMaxWidth().semantics(mergeDescendants = true) {}, verticalArrangement = Arrangement.spacedBy(4.dp)) { Text(label, color = MaterialTheme.colorScheme.onSurfaceVariant, style = MaterialTheme.typography.bodySmall); Text(value) } }
@Composable internal fun PasswordField(label: String, text: String, enabled: Boolean, onChange: (String) -> Unit) {
    var visible by remember { mutableStateOf(false) }
    OutlinedTextField(text, onChange, Modifier.fillMaxWidth(), enabled = enabled, label = { Text(label) }, singleLine = true,
        visualTransformation = if (visible) VisualTransformation.None else PasswordVisualTransformation(), keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password),
        trailingIcon = { IconButton(onClick = { visible = !visible }, Modifier.sizeIn(minWidth = 48.dp, minHeight = 48.dp)) { Icon(if (visible) Icons.Outlined.VisibilityOff else Icons.Outlined.Visibility, if (visible) "Ocultar $label" else "Mostrar $label") } })
}
@Composable private fun LoginScreen(busy: Boolean, action: (AppAction) -> Unit) {
    var user by remember { mutableStateOf("") }; var password by remember { mutableStateOf("") }
    val context = LocalContext.current
    fun browser(path: String) { runCatching { context.startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(BuildConfig.API_BASE_URL + path))) } }
    Page(Modifier.imePadding()) {
        PageTitle("SEU ESPAÇO DE CUIDADO", "Que bom ter você por aqui.", "Entre com sua conta de paciente da CallMed.")
        Panel {
            OutlinedTextField(user, { user = it.take(256) }, Modifier.fillMaxWidth(), label = { Text("CPF ou e-mail") }, singleLine = true, enabled = !busy,
                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Email, imeAction = ImeAction.Next))
            PasswordField("Senha", password, !busy) { password = it.take(256) }
            Primary(if (busy) "Entrando…" else "Entrar na minha conta", !busy) { action(AppAction.Login(user, password)) }
            TextButton(onClick = { browser("/Identity/Account/ForgotPassword") }, Modifier.heightIn(min = 48.dp), enabled = !busy) { Text("Esqueci minha senha") }
        }
        Secondary("Criar conta no site", !busy) { browser("/Identity/Account/Register") }
        Notice("A criação da conta e a recuperação de senha abrem o site da CallMed no navegador.")
    }
}
