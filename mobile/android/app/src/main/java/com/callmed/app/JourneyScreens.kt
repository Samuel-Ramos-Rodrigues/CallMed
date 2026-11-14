package com.callmed.app

import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.semantics.*
import androidx.compose.ui.unit.dp

@Composable
internal fun JourneyScreen(s: UiState, action: (AppAction) -> Unit) {
    val appointment = s.journeyAppointment
    val journey = s.journey
    Page {
        PageTitle("SUA CONSULTA", "Preparar e acompanhar")
        if (appointment == null || journey == null) {
            Muted("Atualize para carregar sua consulta.")
        } else {
            Panel {
                DoctorInfo(appointment.medico, appointment.especialidade)
                Text("${dateLabel(appointment.data)} às ${appointment.horario}")
                Text(journey.text("etapa"), Modifier.semantics { liveRegion = LiveRegionMode.Polite })
                if (journey.text("sala").isNotBlank()) Text("Sala: ${journey.text("sala")}")
            }
            if (!journey.optBoolean("encerrada")) {
                Panel {
                    Text("Confirme sua presença", style = MaterialTheme.typography.titleMedium)
                    if (journey.optBoolean("presencaConfirmada")) {
                        Text("Presença confirmada.")
                    } else {
                        Muted("Seu horário já está agendado. Avise que pretende comparecer.")
                        Primary("Confirmar presença", !s.busy) { action(AppAction.JourneyMove("confirmar")) }
                    }
                }
                Panel {
                    Text("1. Confira seu pré-cadastro", style = MaterialTheme.typography.titleMedium)
                    val payment = if (appointment.pagamento == "Convenio") "Convênio: ${appointment.convenio}" else "Atendimento particular"
                    Text("${s.profile?.nome.orEmpty()}\n${s.profile?.telefone.orEmpty()}\n$payment")
                    Secondary("Corrigir meus dados", !s.busy) { action(AppAction.Open(Screen.EDIT_PROFILE)) }
                    var confirmed by remember(appointment.id) { mutableStateOf(false) }
                    Row {
                        Checkbox(confirmed, { confirmed = it }, enabled = !s.busy)
                        Text("Conferi meus dados e o pagamento ou convênio.", Modifier.weight(1f).padding(top = 12.dp))
                    }
                    Primary(if (journey.optBoolean("cadastroConferido")) "Conferir novamente" else "Confirmar pré-cadastro", !s.busy && confirmed) {
                        action(AppAction.JourneyMove("cadastro"))
                    }
                }
                Panel {
                    Text("2. Check-in antecipado", style = MaterialTheme.typography.titleMedium)
                    Muted(if(journey.optBoolean("teleconsulta")) "Disponível a partir de 24 horas antes. Para o atendimento online, entre na sala pelo botão desta consulta." else "Disponível a partir de 24 horas antes. Ao chegar, identifique-se na recepção para validar sua presença.")
                    if (journey.optBoolean("checkIn")) {
                        Text("Check-in registrado.")
                    } else {
                        Primary("Fazer check-in antecipado", !s.busy && journey.optBoolean("cadastroConferido")) {
                            action(AppAction.JourneyMove("checkin"))
                        }
                    }
                }
            }
            Panel {
                val online = journey.optBoolean("teleconsulta")
                Text(if (online) "Sua sala de consulta online" else "Preparação clínica", style = MaterialTheme.typography.titleMedium)
                Muted("Responda às perguntas publicadas pelo seu profissional e consulte os registros disponíveis nesta sessão.")
                Secondary("Preparar consulta", !s.busy) { action(AppAction.Open(Screen.CLINICAL)) }
                if(online) Primary("Entrar na sala", !s.busy) { action(AppAction.Open(Screen.VIDEO)) }
                TextButton(onClick={action(AppAction.ClinicalPortal)},enabled=!s.busy,modifier=Modifier.heightIn(min=48.dp)){Text("Usar navegador como alternativa")}

            }
        }
        Secondary("Atualizar etapas", !s.busy) { action(AppAction.Refresh) }
        TextButton(onClick = { action(AppAction.Navigate(Tab.CONSULTAS)) }, Modifier.heightIn(min = 48.dp)) {
            Text("Voltar às consultas")
        }
    }
}

@Composable
internal fun WaitlistScreen(s: UiState, action: (AppAction) -> Unit) {
    var selected by remember { mutableStateOf<Doctor?>(null) }
    var period by remember { mutableStateOf("Qualquer") }
    Page {
        PageTitle("ENCAIXES", "Ofertas e lista de espera", "A vaga só será agendada depois que você aceitar.")
        if (s.waitOffers.isEmpty()) Notice("Nenhuma oferta ativa no momento.")
        s.waitOffers.forEach { offer ->
            Panel {
                Text(offer.text("medico"), style = MaterialTheme.typography.titleMedium)
                Text("${dateLabel(offer.text("data"))} às ${offer.text("horario")}")
                Muted("Oferta temporária. O prazo será conferido ao aceitar.")
                Primary("Aceitar e agendar", !s.busy) { action(AppAction.Offer(offer.text("id"), true)) }
                Secondary("Recusar esta vaga", !s.busy) { action(AppAction.Offer(offer.text("id"), false)) }
            }
        }
        Secondary("Atualizar ofertas", !s.busy) { action(AppAction.Waitlist) }
        Panel {
            Text("Entrar na lista de espera", style = MaterialTheme.typography.titleMedium)
            Muted("Escolha profissional e período. Buscaremos vagas compatíveis nos próximos 90 dias.")
            listOf("Qualquer", "Manhã", "Tarde", "Noite").forEach { option ->
                FilterChip(selected = period == option, onClick = { period = option }, label = { Text(option) },
                    enabled = !s.busy, modifier = Modifier.heightIn(min = 48.dp))
            }
            s.doctors.forEach { doctor ->
                FilterChip(selected = selected?.id == doctor.id, onClick = { selected = doctor },
                    label = { Text("${doctor.nome} · ${doctor.especialidade}") }, enabled = !s.busy,
                    modifier = Modifier.fillMaxWidth().heightIn(min = 48.dp))
            }
            Primary("Registrar interesse", !s.busy && selected != null) {
                selected?.let { action(AppAction.WaitDoctor(it, period)) }
            }
        }
    }
}
