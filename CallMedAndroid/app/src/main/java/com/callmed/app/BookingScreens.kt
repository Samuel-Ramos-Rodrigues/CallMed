@file:OptIn(androidx.compose.foundation.layout.ExperimentalLayoutApi::class)
package com.callmed.app

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.selection.selectable
import androidx.compose.foundation.selection.selectableGroup
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.semantics.*
import androidx.compose.ui.unit.dp
import java.time.LocalDate
import java.time.format.DateTimeFormatter
import java.util.Locale

@Composable
internal fun BookingScreen(s: UiState, action: (AppAction) -> Unit) {
    val b = s.booking
    val specialtyList = s.doctors.map { it.especialidade }.filter(String::isNotBlank).distinct().sorted()
    var shownDates by remember(b.doctor?.id) { mutableIntStateOf(12) }
    val title = when(b.stage) { 1 -> "Qual especialidade?"; 2 -> "Escolha o profissional"; 3 -> "Qual dia fica melhor?"; 4 -> "Escolha seu horário"; else -> "Confira antes de confirmar" }
    Page {
        FlowRow(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalArrangement = Arrangement.spacedBy(4.dp)) {
            Back(!s.busy) { action(AppAction.Back) }
            Text("Etapa ${b.stage} de 5", Modifier.padding(vertical = 12.dp).semantics { liveRegion = LiveRegionMode.Polite }, style = MaterialTheme.typography.bodySmall)
        }
        LinearProgressIndicator(progress = { b.stage / 5f }, Modifier.fillMaxWidth().semantics { contentDescription = "Etapa ${b.stage} de 5 do agendamento" })
        PageTitle(if (b.movingId == null) "AGENDAR CONSULTA" else "REMARCAR CONSULTA", title,
            when(b.stage) { 1 -> "Comece pela área de atendimento, como no site."; 2 -> b.specialty; 3 -> b.doctor?.nome.orEmpty(); 4 -> "${dateLabel(b.date)} · ${b.doctor?.nome.orEmpty()}"; else -> "Revise profissional, data e horário antes de confirmar." })
        when(b.stage) {
            1 -> {
                if (specialtyList.isEmpty() && !s.busy) Panel { Text("Nenhuma especialidade disponível."); Muted("A clínica precisa ter profissionais ativos para disponibilizar o agendamento.") }
                ChoiceGrid(specialtyList, columns = 2) { specialty ->
                    Choice(b.specialty == specialty, s.busy, specialty, "", "specialty-$specialty") { action(AppAction.Specialty(specialty)) }
                }
                Primary("Continuar", !s.busy && b.specialty.isNotBlank()) { action(AppAction.Next) }
            }
            2 -> {
                val doctors = s.doctors.filter { it.especialidade == b.specialty }
                if (doctors.isEmpty() && !s.busy) Panel { Muted("Nenhum médico ativo nessa especialidade. Volte para escolher outra área.") }
                doctors.forEach { doctor -> Choice(b.doctor?.id == doctor.id, s.busy, doctor.nome, doctor.especialidade, "doctor-${doctor.id}") { action(AppAction.SelectDoctor(doctor)) } }
                Secondary("Buscar a primeira vaga", !s.busy) { action(AppAction.FirstSlot) }
                Notice("A primeira vaga procura os próximos 90 dias e ainda passa pela sua revisão.")
            }
            3 -> {
                if (b.movingId != null) Notice("O profissional e a forma de atendimento serão mantidos.")
                if (b.dates.isEmpty() && !s.busy) Panel { Text("Nenhuma data disponível."); Muted("Escolha outro profissional ou consulte a equipe da clínica.") }
                ChoiceGrid(b.dates.take(shownDates), columns = 3) { date ->
                    val label = runCatching { LocalDate.parse(date).format(DateTimeFormatter.ofPattern("EEE, dd/MM", Locale.forLanguageTag("pt-BR"))) }.getOrDefault(dateLabel(date))
                    Choice(b.date == date, s.busy, label, "", "date-$date", description = longDateLabel(date)) { action(AppAction.Date(date)) }
                }
                if (b.dates.size > shownDates) Secondary("Mostrar mais datas", !s.busy) { shownDates += 12 }
                Primary("Continuar", !s.busy && b.date.isNotBlank()) { action(AppAction.Next) }
            }
            4 -> {
                if (b.hours.isEmpty() && !s.busy) Panel { Text("Não há horários livres nesse dia."); Muted("Volte e escolha outra data.") }
                ChoiceGrid(b.hours, columns = 3) { hour -> Choice(b.hour == hour, s.busy, hour, "", "hour-$hour", description = "$hour, horário disponível") { action(AppAction.Hour(hour)) } }
                Primary("Continuar", !s.busy && b.hour.isNotBlank()) { action(AppAction.Next) }
            }
            else -> {
                b.doctor?.let { doctor -> Panel { DoctorInfo(doctor.nome, doctor.especialidade); HorizontalDivider(); Detail("Data", longDateLabel(b.date)); Detail("Horário", b.hour); Detail("Local", "Atendimento na clínica") } }
                if (b.movingId == null) {
                    Panel {
                        SectionTitle("Forma de atendimento")
                        RadioChoice("Particular", b.payment == "Particular", !s.busy) { action(AppAction.Payment("Particular")) }
                        if (b.eligibility?.valid == true) {
                            RadioChoice("Convênio cadastrado", b.payment == "Convenio", !s.busy && b.eligibility.eligible) { action(AppAction.Payment("Convenio")) }
                            if (b.eligibility.message.isNotBlank()) Notice(b.eligibility.message)
                        } else Muted("Atendimento particular. Se tiver convênio, atualize os dados da sua conta.")
                    }
                } else Notice("A forma de atendimento permanece ${if (b.payment == "Convenio") "por convênio" else "particular"}.")
                Primary(if (b.movingId == null) "Agendar e confirmar" else "Confirmar remarcação", !s.busy, Icons.Outlined.Check) { action(AppAction.Book) }
            }
        }
    }
}
@Composable internal fun <T> ChoiceGrid(items: List<T>, columns: Int, content: @Composable (T) -> Unit) {
    BoxWithConstraints(Modifier.fillMaxWidth()) {
        val scale = LocalDensity.current.fontScale
        val count = if (scale >= 1.6f || maxWidth < 250.dp) 1 else if (scale >= 1.2f) minOf(columns, 2) else columns
        Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
            items.chunked(count).forEach { row -> Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                row.forEach { item -> Box(Modifier.weight(1f)) { content(item) } }
                repeat(count - row.size) { Spacer(Modifier.weight(1f)) }
            } }
        }
    }
}
@Composable internal fun Choice(selected: Boolean, busy: Boolean, title: String, subtitle: String, tag: String, description: String? = null, choose: () -> Unit) {
    Surface(Modifier.fillMaxWidth().heightIn(min = 54.dp).clipChoice(selected, busy, choose).testTag(tag)
        .then(if (description != null) Modifier.semantics { contentDescription = description } else Modifier),
        shape = RoundedCornerShape(15.dp), color = if (selected) MaterialTheme.colorScheme.primaryContainer else MaterialTheme.colorScheme.surface,
        border = BorderStroke(if (selected) 2.dp else 1.dp, if (selected) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.outline)) {
        Column(Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
            Text(title)
            if (subtitle.isNotBlank()) Muted(subtitle)
            if (selected) Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(5.dp)) { Icon(Icons.Outlined.Check, null, Modifier.size(17.dp)); Text("Selecionado", style = MaterialTheme.typography.labelSmall) }
        }
    }
}
private fun Modifier.clipChoice(selected: Boolean, busy: Boolean, choose: () -> Unit): Modifier = this.selectable(selected = selected, enabled = !busy, role = Role.RadioButton, onClick = choose)
@Composable internal fun RadioChoice(label: String, selected: Boolean, enabled: Boolean, click: () -> Unit) {
    Row(Modifier.fillMaxWidth().heightIn(min = 56.dp).selectable(selected, enabled = enabled, role = Role.RadioButton, onClick = click).padding(vertical = 8.dp), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(10.dp)) {
        Text(label, Modifier.weight(1f), color = if (enabled) MaterialTheme.colorScheme.onSurface else MaterialTheme.colorScheme.onSurfaceVariant)
        RadioButton(selected, onClick = null, enabled = enabled)
    }
}
@Composable internal fun BookingSuccess(s: UiState, action: (AppAction) -> Unit) {
    val a = s.appointments.firstOrNull { it.id == s.bookedId }
    Page {
        Icon(Icons.Outlined.CheckCircleOutline, null, Modifier.size(38.dp), tint = MaterialTheme.colorScheme.secondary)
        PageTitle("TUDO CERTO", if (s.booking.movingId == null) "Consulta confirmada." else "Consulta remarcada.", "Seu horário aparece em Minhas consultas e no site.")
        if (a != null) Panel { StatusLabel(a.status); DoctorInfo(a.medico, a.especialidade); Detail("Data e horário", "${dateLabel(a.data)} às ${a.horario}") }
        Primary("Ver minhas consultas", !s.busy) { action(AppAction.Navigate(Tab.CONSULTAS)) }
    }
}
