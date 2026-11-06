@file:OptIn(androidx.compose.material3.ExperimentalMaterial3Api::class, androidx.compose.foundation.layout.ExperimentalLayoutApi::class)
package com.callmed.app

import android.app.Activity
import android.os.Bundle
import android.content.Intent
import android.net.Uri
import android.graphics.BitmapFactory
import androidx.activity.ComponentActivity
import androidx.activity.enableEdgeToEdge
import androidx.activity.compose.setContent
import androidx.activity.compose.BackHandler
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.text.selection.SelectionContainer
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalView
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.buildAnnotatedString
import androidx.compose.ui.text.withStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.*
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.core.view.WindowCompat
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.compose.LocalLifecycleOwner
import androidx.lifecycle.repeatOnLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import kotlinx.coroutines.delay
import java.time.LocalDate
import java.time.ZoneOffset
import java.time.Instant

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent { val vm: CallMedViewModel = viewModel(); val state by vm.state.collectAsStateWithLifecycle(); CallMedTheme(state.profile?.tema ?: "dark") { CallMedApp(state, vm) } }
    }
}
private val darkColors = darkColorScheme(primary = Color(0xFF9D94FF), onPrimary = Color(0xFF171335), primaryContainer = Color(0xFF302B60), onPrimaryContainer = Color(0xFFE6E2FF),
    background = Color(0xFF0B0F20), onBackground = Color(0xFFF1F2FF), surface = Color(0xFF161D33), onSurface = Color(0xFFF1F2FF),
    surfaceVariant = Color(0xFF202943), onSurfaceVariant = Color(0xFFBCC5DF), outline = Color(0xFF3A4565), outlineVariant = Color(0xFF2F3B58), error = Color(0xFFFFB0B9))
private val lightColors = lightColorScheme(primary = Color(0xFF5146CF), onPrimary = Color.White, primaryContainer = Color(0xFFE9E5FF), onPrimaryContainer = Color(0xFF251A69),
    background = Color(0xFFF3F5FC), onBackground = Color(0xFF18213B), surface = Color.White, onSurface = Color(0xFF18213B),
    surfaceVariant = Color(0xFFE9EDFA), onSurfaceVariant = Color(0xFF55617A), outline = Color(0xFF76829D), outlineVariant = Color(0xFFD4DCEC))
@Composable private fun CallMedTheme(preference: String, content: @Composable () -> Unit) {
    val dark = preference == "dark" || (preference == "system" && isSystemInDarkTheme())
    val view = LocalView.current
    SideEffect { (view.context as? Activity)?.window?.let { window -> WindowCompat.getInsetsController(window, view).apply { isAppearanceLightStatusBars = !dark; isAppearanceLightNavigationBars = !dark } } }
    MaterialTheme(colorScheme = if (dark) darkColors else lightColors, shapes = Shapes(medium = RoundedCornerShape(16.dp), large = RoundedCornerShape(22.dp)), content = content)
}
@Composable private fun CallMedApp(s: UiState, vm: CallMedViewModel) {
    val snackbar = remember { SnackbarHostState() }
    LaunchedEffect(s.info) { s.info?.takeIf { it.isNotBlank() }?.let { snackbar.showSnackbar(it); vm.dismissInfo() } }
    if (s.error != null) AlertDialog(onDismissRequest = vm::dismissError, title = { Text("Confira esta informação") }, text = { Text(s.error) }, confirmButton = { TextButton(onClick = vm::dismissError) { Text("Entendi") } })
    BackHandler(enabled = s.authenticated && s.tab != Tab.HOME && !s.busy) { vm.navigate(Tab.HOME) }
    Surface(modifier = Modifier.fillMaxSize(), color = MaterialTheme.colorScheme.background) {
        when {
            s.restoring -> Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) { Column(horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(20.dp)) { Brand(); CircularProgressIndicator(); Text("Abrindo seu espaço...") } }
            !s.authenticated -> Scaffold(snackbarHost = { SnackbarHost(snackbar) }, containerColor = MaterialTheme.colorScheme.background) { padding -> LoginScreen(s.busy, vm::login, Modifier.padding(padding)) }
            else -> {
                val keyboardOpen = WindowInsets.isImeVisible
                Scaffold(containerColor = MaterialTheme.colorScheme.background,
                    snackbarHost = { SnackbarHost(snackbar) },
                    topBar = { Column { TopAppBar(title = { Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(10.dp)) { Image(painterResource(R.drawable.logo_callmed), null, Modifier.size(31.dp)); Text(s.tab.titulo, style = MaterialTheme.typography.titleLarge, fontWeight = FontWeight.Bold) } }, actions = { if (s.tab != Tab.CHAT) IconButton(onClick = vm::refresh, enabled = !s.busy) { Icon(Icons.Outlined.Refresh, "Atualizar dados") } else IconButton(onClick = vm::refreshChat, enabled = !s.busy) { Icon(Icons.Outlined.Refresh, "Atualizar conversa") } }, colors = TopAppBarDefaults.topAppBarColors(containerColor = MaterialTheme.colorScheme.background)); if (s.busy) LinearProgressIndicator(Modifier.fillMaxWidth()) } },
                    bottomBar = { if (!keyboardOpen) NavigationBar(containerColor = MaterialTheme.colorScheme.surface) { Tab.entries.forEach { tab -> NavigationBarItem(selected = s.tab == tab, onClick = { if (!s.busy) vm.navigate(tab) }, icon = { Icon(tabIcon(tab), null) }, label = { Text(tab.menu, maxLines = 1, fontSize = 11.sp) }) } } }
                ) { padding ->
                    Box(Modifier.fillMaxSize().padding(padding).consumeWindowInsets(padding)) {
                        if (s.profile == null) ScreenColumn { InfoCard("Vamos atualizar seus dados", "Verifique a conexão para carregar suas consultas e seu perfil."); Button(onClick = vm::restore, enabled = !s.busy) { Text("Tentar novamente") }; TextButton(onClick = vm::logout, enabled = !s.busy) { Text("Sair da conta") } }
                        else when (s.tab) {
                            Tab.HOME -> HomeScreen(s, vm)
                            Tab.CONSULTAS -> AppointmentsScreen(s, vm)
                            Tab.AGENDAR -> ScheduleScreen(s, vm)
                            Tab.CHAT -> ChatScreen(s, vm)
                            Tab.CONTA -> AccountScreen(s, vm)
                        }
                    }
                }
            }
        }
    }
}
private fun tabIcon(tab: Tab): ImageVector = when (tab) { Tab.HOME -> Icons.Outlined.Home; Tab.CONSULTAS -> Icons.Outlined.CalendarMonth; Tab.AGENDAR -> Icons.Outlined.AddCircleOutline; Tab.CHAT -> Icons.Outlined.ChatBubbleOutline; Tab.CONTA -> Icons.Outlined.PersonOutline }
@Composable private fun Brand() { Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(12.dp)) { Image(painterResource(R.drawable.logo_callmed), null, Modifier.size(54.dp)); Column { Text("CallMed", fontWeight = FontWeight.ExtraBold, fontSize = 29.sp); Text("CUIDADO EM CONEXÃO", color = MaterialTheme.colorScheme.onSurfaceVariant, fontSize = 10.sp, letterSpacing = 1.sp) } } }
@Composable private fun ScreenColumn(content: @Composable ColumnScope.() -> Unit) { Column(Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(20.dp), verticalArrangement = Arrangement.spacedBy(18.dp), content = content) }
@Composable private fun Panel(modifier: Modifier = Modifier, content: @Composable ColumnScope.() -> Unit) { Card(modifier.fillMaxWidth(), shape = RoundedCornerShape(20.dp), colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface), border = androidx.compose.foundation.BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant)) { Column(Modifier.padding(20.dp), verticalArrangement = Arrangement.spacedBy(13.dp), content = content) } }
@Composable private fun Muted(text: String) { Text(text, color = MaterialTheme.colorScheme.onSurfaceVariant, style = MaterialTheme.typography.bodyMedium) }
@Composable private fun Eyebrow(text: String) { Text(text.uppercase(), fontSize = 10.sp, fontWeight = FontWeight.Bold, letterSpacing = 1.4.sp, color = MaterialTheme.colorScheme.primary) }
@Composable private fun InfoCard(title: String, text: String) { Panel { Text(title, style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold); Muted(text) } }
@Composable private fun LoginScreen(busy: Boolean, login: (String, String) -> Unit, modifier: Modifier = Modifier) {
    val context = LocalContext.current; var user by remember { mutableStateOf("") }; var password by remember { mutableStateOf("") }; var visible by remember { mutableStateOf(false) }
    Column(modifier.fillMaxSize().imePadding().verticalScroll(rememberScrollState()).padding(horizontal = 26.dp, vertical = 42.dp), verticalArrangement = Arrangement.spacedBy(26.dp)) {
        Brand(); Spacer(Modifier.height(12.dp)); Column(verticalArrangement = Arrangement.spacedBy(10.dp)) { Eyebrow("SEU ESPAÇO DE CUIDADO"); Text("Que bom ter\nvocê por aqui.", style = MaterialTheme.typography.headlineLarge, fontWeight = FontWeight.ExtraBold); Muted("Entre com sua conta de paciente para acompanhar suas consultas e falar com a CallMed.") }
        Panel {
            OutlinedTextField(user, { user = it }, Modifier.fillMaxWidth(), label = { Text("CPF ou e-mail") }, singleLine = true, enabled = !busy, keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Email, imeAction = ImeAction.Next), leadingIcon = { Icon(Icons.Outlined.PersonOutline, null) })
            OutlinedTextField(password, { password = it }, Modifier.fillMaxWidth(), label = { Text("Senha") }, singleLine = true, enabled = !busy, visualTransformation = if (visible) VisualTransformation.None else PasswordVisualTransformation(), keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password, imeAction = ImeAction.Done), keyboardActions = KeyboardActions(onDone = { if (!busy) login(user, password) }), leadingIcon = { Icon(Icons.Outlined.LockOpen, null) }, trailingIcon = { IconButton(onClick = { visible = !visible }) { Icon(if (visible) Icons.Outlined.VisibilityOff else Icons.Outlined.Visibility, if (visible) "Ocultar senha" else "Mostrar senha") } })
            Button(onClick = { login(user, password) }, Modifier.fillMaxWidth().heightIn(min = 50.dp), enabled = !busy) { if (busy) CircularProgressIndicator(Modifier.size(20.dp), strokeWidth = 2.dp) else Text("Entrar na minha conta", fontWeight = FontWeight.Bold) }
            TextButton(onClick = { openBrowser(context, "/Identity/Account/ForgotPassword") }, Modifier.align(Alignment.CenterHorizontally), enabled = !busy) { Text("Esqueci minha senha") }
        }
        Column(horizontalAlignment = Alignment.CenterHorizontally, modifier = Modifier.fillMaxWidth()) { Muted("Ainda não tem conta de paciente?"); TextButton(onClick = { openBrowser(context, "/Identity/Account/Register") }, enabled = !busy) { Text("Criar minha conta") } }
    }
}
private fun openBrowser(context: android.content.Context, path: String) { runCatching { context.startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(BuildConfig.API_BASE_URL + path))) } }
@Composable private fun HomeScreen(s: UiState, vm: CallMedViewModel) {
    val profile = s.profile!!; val next = s.appointments.filter { it.futura }.minWithOrNull(compareBy<Appointment> { it.data }.thenBy { it.horario })
    ScreenColumn {
        Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.SpaceBetween) { Column(Modifier.weight(1f)) { Eyebrow("SEU ESPAÇO"); Text("Olá, ${profile.primeiroNome}.", style = MaterialTheme.typography.headlineMedium, fontWeight = FontWeight.Bold); Muted("Qual é o seu próximo cuidado?") }; Avatar(profile, s.photo, 54) }
        Panel {
            Eyebrow("SUA PRÓXIMA CONSULTA")
            if (next != null) { Text(next.medico, style = MaterialTheme.typography.titleLarge, fontWeight = FontWeight.Bold); Muted(next.especialidade); Row(horizontalArrangement = Arrangement.spacedBy(8.dp), verticalAlignment = Alignment.CenterVertically) { Icon(Icons.Outlined.CalendarMonth, null, tint = MaterialTheme.colorScheme.primary); Text("${dateLabel(next.data)} · ${next.horario}", fontWeight = FontWeight.Bold) }; StatusChip(next.status); OutlinedButton(onClick = { vm.navigate(Tab.CONSULTAS) }, Modifier.fillMaxWidth()) { Text("Ver minhas consultas") } }
            else { Text("Um horário para cuidar de você.", style = MaterialTheme.typography.titleLarge, fontWeight = FontWeight.Bold); Muted("Escolha o profissional e veja as vagas disponíveis."); Button(onClick = { vm.navigate(Tab.AGENDAR) }, Modifier.fillMaxWidth()) { Text("Agendar uma consulta") } }
        }
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(12.dp)) { Shortcut("Agendar", "Escolher um horário", Icons.Outlined.AddCircleOutline, Modifier.weight(1f)) { vm.navigate(Tab.AGENDAR) }; Shortcut("Atendimento", "Falar com a CallMed", Icons.Outlined.ChatBubbleOutline, Modifier.weight(1f)) { vm.navigate(Tab.CHAT) } }
        Panel { Eyebrow("DO SEU JEITO"); Text("Seu atendimento, suas preferências.", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold); Muted(if (profile.temConvenio) "Convênio: ${profile.nomeConvenio}. A cobertura depende da consulta e da conferência da clínica." else "Seus dados e os canais de aviso podem ser atualizados na sua conta."); TextButton(onClick = { vm.navigate(Tab.CONTA) }) { Text("Conferir meu perfil") } }
    }
}
@Composable private fun Shortcut(title: String, description: String, icon: ImageVector, modifier: Modifier, click: () -> Unit) { Card(onClick = click, modifier = modifier, colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.primaryContainer), shape = RoundedCornerShape(20.dp)) { Column(Modifier.padding(17.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) { Icon(icon, null, tint = MaterialTheme.colorScheme.onPrimaryContainer, modifier = Modifier.size(28.dp)); Text(title, color = MaterialTheme.colorScheme.onPrimaryContainer, fontWeight = FontWeight.Bold); Text(description, color = MaterialTheme.colorScheme.onPrimaryContainer, fontSize = 12.sp) } } }
@Composable private fun StatusChip(status: String) { Surface(color = if (status == "Cancelada") MaterialTheme.colorScheme.errorContainer else MaterialTheme.colorScheme.primaryContainer, shape = RoundedCornerShape(9.dp)) { Text(status, Modifier.padding(horizontal = 10.dp, vertical = 5.dp), fontSize = 11.sp, fontWeight = FontWeight.Bold) } }
@Composable private fun AppointmentsScreen(s: UiState, vm: CallMedViewModel) {
    var history by remember { mutableStateOf(false) }; var confirm by remember { mutableStateOf<Pair<Appointment, String>?>(null) }
    val list = s.appointments.filter { if (history) !it.futura else it.futura }.let { if (history) it else it.sortedWith(compareBy<Appointment> { a -> a.data }.thenBy { a -> a.horario }) }
    LazyColumn(Modifier.fillMaxSize(), contentPadding = PaddingValues(20.dp), verticalArrangement = Arrangement.spacedBy(16.dp)) {
        item { Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) { FilterChip(!history, { history = false }, { Text("Próximas") }); FilterChip(history, { history = true }, { Text("Histórico") }) } }
        if (list.isEmpty()) item { InfoCard(if (history) "Seu histórico começa aqui" else "Nenhuma consulta por aqui", if (history) "As consultas anteriores e canceladas aparecem nesta área." else "Agende uma consulta e acompanhe tudo por aqui."); if (!history) Button(onClick = { vm.navigate(Tab.AGENDAR) }, Modifier.fillMaxWidth().padding(top = 16.dp)) { Text("Encontrar um horário") } }
        items(list, key = { it.id }) { appointment ->
            Panel {
                Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) { Text("${dateLabel(appointment.data)} · ${appointment.horario}", fontWeight = FontWeight.Bold); StatusChip(appointment.status) }
                Text(appointment.medico, style = MaterialTheme.typography.titleLarge, fontWeight = FontWeight.Bold); Muted(appointment.especialidade)
                Muted(if (appointment.pagamento == "Convenio") "Convênio: ${appointment.convenio}" else "Atendimento particular")
                if (appointment.observacaoConvenio.isNotBlank()) Text(appointment.observacaoConvenio, fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                FlowRow(horizontalArrangement = Arrangement.spacedBy(8.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                    if (appointment.podeConfirmar) Button(onClick = { confirm = appointment to "confirmar" }, enabled = !s.busy) { Text("Confirmar presença") }
                    if (appointment.podeRemarcar) OutlinedButton(onClick = { vm.startReschedule(appointment) }, enabled = !s.busy) { Text("Remarcar") }
                    if (appointment.podeCancelar) TextButton(onClick = { confirm = appointment to "cancelar" }, enabled = !s.busy) { Text("Cancelar", color = MaterialTheme.colorScheme.error) }
                }
            }
        }
    }
    confirm?.let { (appointment, action) -> AlertDialog(onDismissRequest = { confirm = null }, title = { Text(if (action == "cancelar") "Cancelar esta consulta?" else "Confirmar sua presença?") }, text = { Text("${appointment.medico}\n${dateLabel(appointment.data)} às ${appointment.horario}" + if (action == "cancelar") "\n\nA vaga será liberada para outro paciente." else "") }, dismissButton = { TextButton(onClick = { confirm = null }) { Text("Voltar") } }, confirmButton = { TextButton(onClick = { confirm = null; vm.appointmentAction(appointment, action) }) { Text(if (action == "cancelar") "Confirmar cancelamento" else "Confirmar presença") } }) }
}
@Composable private fun ScheduleScreen(s: UiState, vm: CallMedViewModel) {
    var search by remember { mutableStateOf("") }; var confirm by remember { mutableStateOf(false) }; var payment by remember(s.movingId, s.profile?.convenioValido) { mutableStateOf(if (s.profile?.convenioValido == true) "Convenio" else "Particular") }
    val doctor = s.doctor
    if (doctor == null) Column(Modifier.fillMaxSize().padding(horizontal = 20.dp)) {
        Text("Com quem vamos agendar?", style = MaterialTheme.typography.headlineSmall, fontWeight = FontWeight.Bold, modifier = Modifier.padding(top = 16.dp, bottom = 10.dp))
        OutlinedTextField(search, { search = it }, Modifier.fillMaxWidth(), label = { Text("Médico ou especialidade") }, singleLine = true, leadingIcon = { Icon(Icons.Outlined.Search, null) })
        val doctors = s.doctors.filter { it.nome.contains(search, true) || it.especialidade.contains(search, true) }
        LazyColumn(Modifier.weight(1f), contentPadding = PaddingValues(vertical = 18.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            if (doctors.isEmpty() && !s.busy) item { InfoCard("Nenhum profissional encontrado", "Tente outra busca ou fale com a clínica pelo atendimento.") }
            items(doctors, key = { it.id }) { d -> Card(onClick = { if (!s.busy) vm.selectDoctor(d) }, colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface), border = androidx.compose.foundation.BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant)) { Row(Modifier.fillMaxWidth().padding(19.dp), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(14.dp)) { Icon(Icons.Outlined.MedicalServices, null, tint = MaterialTheme.colorScheme.primary); Column(Modifier.weight(1f)) { Text(d.nome, fontWeight = FontWeight.Bold); Text(d.especialidade, fontSize = 13.sp, color = MaterialTheme.colorScheme.onSurfaceVariant); if (d.crm.isNotBlank()) Text(d.crm, fontSize = 11.sp, color = MaterialTheme.colorScheme.onSurfaceVariant) }; Icon(Icons.Outlined.ChevronRight, "Ver horários") } } }
        }
    } else ScreenColumn {
        TextButton(onClick = vm::clearDoctor, enabled = !s.busy) { Icon(Icons.Outlined.ArrowBack, null); Text("Trocar profissional", Modifier.padding(start = 8.dp)) }
        Column { Eyebrow(if (s.movingId != null) "REMARCAR CONSULTA" else "NOVO AGENDAMENTO"); Text(doctor.nome, style = MaterialTheme.typography.headlineSmall, fontWeight = FontWeight.Bold); Muted(doctor.especialidade) }
        Panel {
            Text("Escolha a data", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
            if (s.dates.isEmpty() && !s.busy) { Muted("Não há datas disponíveis nos próximos 90 dias."); TextButton(onClick = { vm.selectDoctor(doctor, s.movingId) }) { Text("Atualizar disponibilidade") } }
            LazyRow(horizontalArrangement = Arrangement.spacedBy(9.dp)) { items(s.dates) { date -> val parsed = LocalDate.parse(date); val selected = date == s.selectedDate
                Surface(onClick = { if (!s.busy) vm.selectDate(date) }, modifier = Modifier.width(76.dp).semantics { contentDescription = longDateLabel(date) }, shape = RoundedCornerShape(14.dp), color = if (selected) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.surfaceVariant) { Column(Modifier.padding(vertical = 13.dp), horizontalAlignment = Alignment.CenterHorizontally) { Text(parsed.dayOfWeek.getDisplayName(java.time.format.TextStyle.SHORT, java.util.Locale.forLanguageTag("pt-BR")).uppercase(), fontSize = 10.sp); Text(parsed.dayOfMonth.toString(), fontWeight = FontWeight.ExtraBold, fontSize = 24.sp); Text(parsed.month.getDisplayName(java.time.format.TextStyle.SHORT, java.util.Locale.forLanguageTag("pt-BR")).uppercase(), fontSize = 10.sp) } }
            } }
            if (s.dates.size > 3) Text("Deslize para ver mais datas.", fontSize = 11.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
        if (s.selectedDate.isNotBlank()) Panel {
            Text("Horários disponíveis", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold); Muted(longDateLabel(s.selectedDate))
            if (s.hours.isEmpty() && !s.busy) Muted("Nenhum horário disponível. Escolha outra data.")
            FlowRow(horizontalArrangement = Arrangement.spacedBy(9.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) { s.hours.forEach { hour -> FilterChip(selected = s.selectedHour == hour, onClick = { vm.selectHour(hour) }, label = { Text(hour) }, enabled = !s.busy) } }
        }
        if (s.selectedHour.isNotEmpty()) Panel {
            Text("Revise seu horário", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold); Text("${dateLabel(s.selectedDate)} às ${s.selectedHour}", color = MaterialTheme.colorScheme.primary, fontWeight = FontWeight.Bold)
            if (s.movingId == null && s.profile?.convenioValido == true) { Text("Como será o atendimento?", fontWeight = FontWeight.Medium); ChoiceRow("Convênio cadastrado", payment == "Convenio") { payment = "Convenio" }; ChoiceRow("Particular", payment == "Particular") { payment = "Particular" }; Muted("A cobertura e eventuais autorizações são conferidas pela clínica.") }
            else Muted(if (s.movingId != null) "A remarcação mantém a forma de atendimento da consulta." else "Atendimento particular. Atualize seu convênio na conta, se necessário.")
            Button(onClick = { confirm = true }, Modifier.fillMaxWidth(), enabled = !s.busy) { Text(if (s.movingId != null) "Revisar remarcação" else "Revisar agendamento") }
        }
    }
    if (confirm && doctor != null) AlertDialog(onDismissRequest = { confirm = false }, title = { Text(if (s.movingId != null) "Confirmar novo horário?" else "Confirmar agendamento?") }, text = { Text("${doctor.nome}\n${doctor.especialidade}\n\n${dateLabel(s.selectedDate)} às ${s.selectedHour}" + if (s.movingId == null) "\n${if (payment == "Convenio") "Convênio" else "Particular"}" else "") }, dismissButton = { TextButton(onClick = { confirm = false }) { Text("Voltar") } }, confirmButton = { Button(onClick = { confirm = false; vm.book(payment) }, enabled = !s.busy) { Text("Confirmar") } })
}
@Composable private fun ChoiceRow(label: String, selected: Boolean, onClick: () -> Unit) { Row(Modifier.fillMaxWidth().clickable(onClick = onClick), verticalAlignment = Alignment.CenterVertically) { RadioButton(selected, onClick); Text(label, style = MaterialTheme.typography.bodyMedium) } }
@Composable private fun Avatar(profile: Patient, photo: ByteArray?, size: Int) {
    val bitmap = remember(photo) { photo?.let { bytes -> runCatching { val bounds = BitmapFactory.Options().apply { inJustDecodeBounds = true }; BitmapFactory.decodeByteArray(bytes, 0, bytes.size, bounds); var sample = 1; while (bounds.outWidth / sample > 512 || bounds.outHeight / sample > 512) sample *= 2; BitmapFactory.decodeByteArray(bytes, 0, bytes.size, BitmapFactory.Options().apply { inSampleSize = sample })?.asImageBitmap() }.getOrNull() } }
    Box(Modifier.size(size.dp).clip(CircleShape).background(MaterialTheme.colorScheme.primaryContainer), contentAlignment = Alignment.Center) { if (bitmap != null) Image(bitmap, "Foto do perfil", Modifier.fillMaxSize(), contentScale = androidx.compose.ui.layout.ContentScale.Crop) else Text(profile.iniciais, fontWeight = FontWeight.ExtraBold, fontSize = (size / 3).sp, color = MaterialTheme.colorScheme.onPrimaryContainer) }
}
@Composable private fun AccountScreen(s: UiState, vm: CallMedViewModel) {
    val p = s.profile!!; var nome by remember(p.nome) { mutableStateOf(p.nome) }; var telefone by remember(p.telefone) { mutableStateOf(p.telefone) }; var nascimento by remember(p.nascimento) { mutableStateOf(inputDate(p.nascimento)) }
    var tem by remember(p.temConvenio) { mutableStateOf(p.temConvenio) }; var convenio by remember(p.nomeConvenio) { mutableStateOf(p.nomeConvenio) }; var numero by remember(p.numeroConvenio) { mutableStateOf(p.numeroConvenio) }; var validade by remember(p.validadeConvenio) { mutableStateOf(inputDate(p.validadeConvenio)) }
    var tema by remember(p.tema) { mutableStateOf(p.tema) }; var whats by remember(p.whatsApp) { mutableStateOf(p.whatsApp) }; var email by remember(p.emailAvisos) { mutableStateOf(p.emailAvisos) }; var sms by remember(p.sms) { mutableStateOf(p.sms) }; var canal by remember(p.canal) { mutableStateOf(p.canal) }
    var showSecurity by remember { mutableStateOf(false) }; var atual by remember { mutableStateOf("") }; var nova by remember { mutableStateOf("") }; var confirmar by remember { mutableStateOf("") }
    val picker = rememberLauncherForActivityResult(ActivityResultContracts.GetContent()) { uri -> if (uri != null) vm.uploadPhoto(uri) }
    ScreenColumn {
        Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(18.dp)) { Avatar(p, s.photo, 76); Column(Modifier.weight(1f)) { Eyebrow("PACIENTE CALLMED"); Text(p.nome, style = MaterialTheme.typography.titleLarge, fontWeight = FontWeight.Bold); TextButton(onClick = { picker.launch("image/*") }, enabled = !s.busy, contentPadding = PaddingValues(0.dp)) { Text("Alterar foto") } } }
        Panel {
            Text("Meus dados", style = MaterialTheme.typography.titleLarge, fontWeight = FontWeight.Bold)
            OutlinedTextField(nome, { nome = it.take(150) }, Modifier.fillMaxWidth(), label = { Text("Nome completo") }, enabled = !s.busy, singleLine = true)
            OutlinedTextField(telefone, { telefone = it.take(30) }, Modifier.fillMaxWidth(), label = { Text("Telefone") }, keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Phone), enabled = !s.busy, singleLine = true)
            ReadOnlyDetail("E-mail", p.email); ReadOnlyDetail("CPF", p.cpf)
            NativeDateField("Nascimento", nascimento, true, !s.busy) { nascimento = it }
            Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.SpaceBetween) { Text("Possuo convênio"); Switch(tem, { tem = it }, enabled = !s.busy) }
            if (tem) { OutlinedTextField(convenio, { convenio = it.take(120) }, Modifier.fillMaxWidth(), label = { Text("Nome do convênio") }, enabled = !s.busy, singleLine = true); OutlinedTextField(numero, { numero = it.take(80) }, Modifier.fillMaxWidth(), label = { Text("Carteirinha") }, enabled = !s.busy, singleLine = true); NativeDateField("Validade do convênio", validade, false, !s.busy) { validade = it } }
            Button(onClick = { vm.saveProfile(nome, telefone, nascimento, tem, convenio, numero, validade) }, Modifier.fillMaxWidth(), enabled = !s.busy) { Text("Salvar meus dados") }
        }
        Panel {
            Text("Preferências", style = MaterialTheme.typography.titleLarge, fontWeight = FontWeight.Bold); Muted("Escolha a aparência e os canais dos avisos da clínica.")
            FlowRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) { listOf("dark" to "Escuro", "light" to "Claro", "system" to "Dispositivo").forEach { (value, label) -> FilterChip(tema == value, { tema = value }, { Text(label) }, enabled = !s.busy) } }
            ToggleRow("WhatsApp", whats, !s.busy) { whats = it }; ToggleRow("E-mail", email, !s.busy) { email = it }; ToggleRow("SMS", sms, !s.busy) { sms = it }
            Text("Canal preferido", style = MaterialTheme.typography.titleSmall)
            FlowRow(horizontalArrangement = Arrangement.spacedBy(6.dp)) { listOf("WhatsApp" to "WhatsApp", "Email" to "E-mail", "SMS" to "SMS", "Telefone" to "Ligação").forEach { (value, label) -> FilterChip(canal == value, { canal = value }, { Text(label) }, enabled = !s.busy) } }
            Button(onClick = { vm.savePreferences(tema, whats, email, sms, canal) }, Modifier.fillMaxWidth(), enabled = !s.busy) { Text("Salvar preferências") }
        }
        Panel {
            Row(Modifier.fillMaxWidth().clickable { showSecurity = !showSecurity }, horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) { Text("Segurança", style = MaterialTheme.typography.titleLarge, fontWeight = FontWeight.Bold); Icon(if (showSecurity) Icons.Outlined.ExpandLess else Icons.Outlined.ExpandMore, if (showSecurity) "Recolher segurança" else "Abrir segurança") }
            if (showSecurity) { Muted("Ao trocar sua senha, seus acessos serão encerrados. Entre novamente com a senha nova."); PasswordField("Senha atual", atual, !s.busy) { atual = it }; PasswordField("Nova senha", nova, !s.busy) { nova = it }; PasswordField("Confirmar nova senha", confirmar, !s.busy) { confirmar = it }; Text("Use pelo menos 8 caracteres, com maiúscula, minúscula, número e símbolo.", fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant); Button(onClick = { vm.changePassword(atual, nova, confirmar) }, Modifier.fillMaxWidth(), enabled = !s.busy && atual.isNotEmpty() && nova.isNotEmpty()) { Text("Alterar senha") } }
            OutlinedButton(onClick = vm::logout, Modifier.fillMaxWidth(), enabled = !s.busy) { Icon(Icons.Outlined.Logout, null); Text("Sair da minha conta", Modifier.padding(start = 8.dp)) }
        }
        Text("CallMed ${BuildConfig.VERSION_NAME}", Modifier.fillMaxWidth(), textAlign = TextAlign.Center, color = MaterialTheme.colorScheme.onSurfaceVariant, fontSize = 11.sp)
    }
}
@Composable private fun ReadOnlyDetail(label: String, value: String) { Column(verticalArrangement = Arrangement.spacedBy(4.dp)) { Text(label, fontSize = 11.sp, color = MaterialTheme.colorScheme.onSurfaceVariant); SelectionContainer { Text(value.ifBlank { "Não informado" }, fontSize = 14.sp) } } }
@Composable private fun PasswordField(label: String, value: String, enabled: Boolean, change: (String) -> Unit) { OutlinedTextField(value, change, Modifier.fillMaxWidth(), label = { Text(label) }, singleLine = true, enabled = enabled, visualTransformation = PasswordVisualTransformation(), keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password)) }
@Composable private fun ToggleRow(label: String, value: Boolean, enabled: Boolean, change: (Boolean) -> Unit) { Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.SpaceBetween) { Text(label); Switch(value, change, enabled = enabled) } }
@Composable private fun NativeDateField(label: String, value: String, birth: Boolean, enabled: Boolean, change: (String) -> Unit) {
    var open by remember { mutableStateOf(false) }
    Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) { OutlinedButton(onClick = { open = true }, Modifier.weight(1f), enabled = enabled) { Icon(Icons.Outlined.CalendarMonth, null); Text("$label: ${value.ifBlank { "Selecionar" }}", Modifier.padding(start = 8.dp), maxLines = 2) }; if (value.isNotBlank()) IconButton(onClick = { change("") }, enabled = enabled) { Icon(Icons.Outlined.Close, "Limpar $label") } }
    if (open) {
        val today = LocalDate.now(); val range = if (birth) (today.year - 120)..today.year else today.year..(today.year + 30)
        val selected = runCatching { isoDate(value)?.let { LocalDate.parse(it) }?.takeIf { it.year in range }?.atStartOfDay()?.toInstant(ZoneOffset.UTC)?.toEpochMilli() }.getOrNull()
        val date = rememberDatePickerState(initialSelectedDateMillis = selected, yearRange = range, selectableDates = object : SelectableDates { override fun isSelectableDate(utcTimeMillis: Long): Boolean { val day = Instant.ofEpochMilli(utcTimeMillis).atZone(ZoneOffset.UTC).toLocalDate(); return if (birth) day <= today else day >= today } })
        DatePickerDialog(onDismissRequest = { open = false }, confirmButton = { TextButton(onClick = { date.selectedDateMillis?.let { change(dateLabel(Instant.ofEpochMilli(it).atZone(ZoneOffset.UTC).toLocalDate().toString())) }; open = false }, enabled = date.selectedDateMillis != null) { Text("Selecionar") } }, dismissButton = { TextButton(onClick = { open = false }) { Text("Voltar") } }) { DatePicker(date) }
    }
}
@Composable private fun ChatScreen(s: UiState, vm: CallMedViewModel) {
    val lifecycle = LocalLifecycleOwner.current.lifecycle; val list = androidx.compose.foundation.lazy.rememberLazyListState(); var draft by remember { mutableStateOf("") }; var lastSent by remember { mutableStateOf("") }
    LaunchedEffect(lifecycle) { lifecycle.repeatOnLifecycle(Lifecycle.State.STARTED) { while (true) { delay(4500); vm.pollChat() } } }
    LaunchedEffect(s.chat.messages.lastOrNull()?.id, s.chat.avaliacaoId, s.busy) { if (!s.busy && s.error != null && draft.isBlank()) draft = lastSent; if (!s.busy) lastSent = ""; if (list.layoutInfo.totalItemsCount > 0) list.animateScrollToItem(list.layoutInfo.totalItemsCount - 1) }
    Column(Modifier.fillMaxSize().imePadding()) {
        Row(Modifier.fillMaxWidth().padding(horizontal = 20.dp, vertical = 9.dp), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) { StatusChip(if (s.chat.modo == "Humano") "Equipe CallMed" else "Assistente IA"); if (!s.chat.encerrada && s.chat.messages.isNotEmpty()) TextButton(onClick = vm::endChat, enabled = !s.busy) { Text("Encerrar e avaliar", fontSize = 12.sp) } }
        LazyColumn(Modifier.weight(1f).fillMaxWidth(), state = list, contentPadding = PaddingValues(horizontal = 18.dp, vertical = 14.dp), verticalArrangement = Arrangement.spacedBy(13.dp)) {
            if (s.chat.messages.isEmpty()) item { InfoCard("Vamos conversar?", "Envie sua dúvida sobre consultas, horários ou atendimento. Você também pode pedir para falar com a equipe.") }
            items(s.chat.messages, key = { it.id }) { message -> ChatBubble(message.texto, message.autor) }
            if (s.busy && lastSent.isNotEmpty()) { item { ChatBubble(lastSent, "paciente") }; item { Row(Modifier.padding(12.dp), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(9.dp)) { CircularProgressIndicator(Modifier.size(16.dp), strokeWidth = 2.dp); Text("Respondendo...", color = MaterialTheme.colorScheme.onSurfaceVariant, fontSize = 12.sp) } } }
            if (s.chat.avaliacaoId != null) item { FeedbackCard(s.chat.avaliacaoId, s.busy, vm::rate) }
            else if (s.chat.encerrada) item { Text("Atendimento encerrado. Envie uma mensagem quando precisar novamente.", fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant, modifier = Modifier.padding(10.dp)) }
        }
        Surface(color = MaterialTheme.colorScheme.surface, tonalElevation = 2.dp) { Row(Modifier.fillMaxWidth().padding(horizontal = 14.dp, vertical = 12.dp), verticalAlignment = Alignment.Bottom, horizontalArrangement = Arrangement.spacedBy(8.dp)) { OutlinedTextField(draft, { draft = it.take(1000) }, Modifier.weight(1f), placeholder = { Text("Digite sua mensagem", fontSize = 14.sp) }, maxLines = 4, enabled = !s.busy); FilledIconButton(onClick = { lastSent = draft.trim(); draft = ""; vm.send(lastSent) }, enabled = draft.isNotBlank() && !s.busy, modifier = Modifier.size(48.dp)) { Icon(Icons.Outlined.Send, "Enviar mensagem") } } }
    }
}
@Composable private fun ChatBubble(text: String, author: String) { val own = author == "paciente"; Row(Modifier.fillMaxWidth(), horizontalArrangement = if (own) Arrangement.End else Arrangement.Start) { Surface(shape = RoundedCornerShape(17.dp), color = if (own) MaterialTheme.colorScheme.primaryContainer else MaterialTheme.colorScheme.surface, modifier = Modifier.widthIn(max = 330.dp).fillMaxWidth(.9f), border = androidx.compose.foundation.BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant)) { Column(Modifier.padding(15.dp), verticalArrangement = Arrangement.spacedBy(7.dp)) { Text(if (own) "Você" else if (author == "equipe") "Equipe CallMed" else "Assistente CallMed", fontSize = 10.sp, fontWeight = FontWeight.Bold, color = MaterialTheme.colorScheme.primary); SelectionContainer { Text(buildAnnotatedString { var start = 0; Regex("\\*\\*(.+?)\\*\\*").findAll(text).forEach { match -> append(text.substring(start, match.range.first)); withStyle(SpanStyle(fontWeight = FontWeight.Bold)) { append(match.groupValues[1]) }; start = match.range.last + 1 }; append(text.substring(start)) }, fontSize = 14.sp, lineHeight = 21.sp) } } } } }
@Composable private fun FeedbackCard(id: String, busy: Boolean, rate: (Int?, String, Boolean) -> Unit) { var note by remember(id) { mutableStateOf<Int?>(null) }; var comment by remember(id) { mutableStateOf("") }; Panel { Eyebrow("ATENDIMENTO ENCERRADO"); Text("Como foi seu atendimento?", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold); Muted("Sua avaliação é opcional. Escolha de 1 (muito ruim) a 5 (muito bom)."); Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceEvenly) { (1..5).forEach { value -> FilterChip(note == value, { note = value }, label = { Text(value.toString()) }, enabled = !busy) } }; OutlinedTextField(comment, { comment = it.take(1000) }, Modifier.fillMaxWidth(), label = { Text("Comentário opcional") }, maxLines = 3, enabled = !busy); Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(10.dp)) { Button(onClick = { rate(note, comment, false) }, enabled = note != null && !busy) { Text("Avaliar") }; TextButton(onClick = { rate(null, "", true) }, enabled = !busy) { Text("Agora não") } } } }
