package com.callmed.app

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.text.selection.SelectionContainer
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.Send
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.semantics.*
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.buildAnnotatedString
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.withStyle
import androidx.compose.ui.text.input.KeyboardCapitalization
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.unit.dp

@Composable internal fun ChatScreen(s: UiState, action: (AppAction) -> Unit) {
    val list = rememberLazyListState()
    val reduce = s.reading.reduceMotion
    LaunchedEffect(s.chat.messages.lastOrNull()?.id, s.chat.avaliacaoId, s.feedbackAccepted) {
        if (list.layoutInfo.totalItemsCount > 0) {
            val last = list.layoutInfo.totalItemsCount - 1
            if (reduce) list.scrollToItem(last) else list.animateScrollToItem(last)
        }
    }
    Column(Modifier.fillMaxSize().imePadding()) {
        Row(Modifier.fillMaxWidth().padding(horizontal = 18.dp, vertical = 7.dp), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
            Text(if (s.chat.modo == "Humano") "Equipe CallMed" else "Assistente CallMed", Modifier.weight(1f), style = MaterialTheme.typography.titleSmall)
            if (!s.chat.encerrada && s.chat.messages.isNotEmpty()) TextButton(onClick = { action(AppAction.EndChat) }, Modifier.heightIn(min = 48.dp), enabled = !s.busy) { Text("Encerrar") }
        }
        LazyColumn(Modifier.weight(1f).fillMaxWidth().testTag("chat-history"), state = list, contentPadding = PaddingValues(horizontal = 18.dp, vertical = 14.dp), verticalArrangement = Arrangement.spacedBy(if (s.reading.spacious) 22.dp else 14.dp)) {
            if (s.chat.messages.isEmpty()) item { Panel { SectionTitle("Como podemos ajudar?"); Muted("Envie sua dúvida sobre consultas, horários ou atendimento. Você também pode pedir para falar com a equipe.") } }
            items(s.chat.messages, key = { it.id }) { message -> ChatBubble(message, message.id == s.chat.messages.lastOrNull()?.id) }
            if (s.busy) item { Row(Modifier.padding(10.dp).semantics { liveRegion = LiveRegionMode.Polite }, verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(10.dp)) { CircularProgressIndicator(Modifier.size(20.dp), strokeWidth = 2.dp); Text("Atualizando atendimento…", style = MaterialTheme.typography.bodySmall) } }
            if (s.chat.avaliacaoId != null) item { Feedback(s, action) }
            else if (s.chat.encerrada) item { Notice("Atendimento encerrado. Envie uma mensagem quando precisar novamente.") }
        }
        Surface(color = MaterialTheme.colorScheme.surface) {
            Column(Modifier.fillMaxWidth().padding(14.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                OutlinedTextField(s.chatDraft, { action(AppAction.ChatDraft(it)) }, Modifier.fillMaxWidth().testTag("chat-draft"), label = { Text("Sua mensagem") }, placeholder = { Text("Digite sua dúvida…") }, maxLines = 4, enabled = !s.busy,
                    keyboardOptions = KeyboardOptions(capitalization = KeyboardCapitalization.Sentences))
                Primary(if (s.busy) "Aguarde…" else "Enviar mensagem", !s.busy && s.chatDraft.isNotBlank(), Icons.AutoMirrored.Outlined.Send) { action(AppAction.Send) }
            }
        }
    }
}
@Composable private fun ChatBubble(message: ChatMessage, latest: Boolean) {
    val own = message.autor == "paciente"
    Row(Modifier.fillMaxWidth(), horizontalArrangement = if (own) Arrangement.End else Arrangement.Start) {
        Surface(Modifier.fillMaxWidth(.94f).then(if (latest && !own) Modifier.semantics { liveRegion = LiveRegionMode.Polite } else Modifier),
            color = if (own) MaterialTheme.colorScheme.primaryContainer else MaterialTheme.colorScheme.surface, shape = RoundedCornerShape(17.dp), border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant)) {
            Column(Modifier.padding(15.dp), verticalArrangement = Arrangement.spacedBy(7.dp)) {
                Text(if (own) "Você" else if (message.autor == "equipe") "Equipe CallMed" else "Assistente CallMed", style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.primary)
                SelectionContainer { Text(buildAnnotatedString {
                    var start = 0
                    Regex("\\*\\*(.+?)\\*\\*").findAll(message.texto).forEach { match ->
                        append(message.texto.substring(start, match.range.first))
                        withStyle(SpanStyle(fontWeight = FontWeight.Medium)) { append(match.groupValues[1]) }
                        start = match.range.last + 1
                    }
                    append(message.texto.substring(start))
                }) }
            }
        }
    }
}
@Composable private fun Feedback(s: UiState, action: (AppAction) -> Unit) {
    Panel {
        if (!s.feedbackAccepted) {
            SectionTitle("Quer avaliar o atendimento?")
            Muted("Sua avaliação é opcional e ajuda a melhorar a CallMed.")
            Primary("Sim, quero avaliar", !s.busy) { action(AppAction.AcceptFeedback) }
            Secondary("Agora não", !s.busy) { action(AppAction.Rate(true)) }
        } else {
            SectionTitle("Como foi seu atendimento?")
            Muted("De 1 (muito ruim) a 5 (muito bom). O comentário é opcional.")
            ChoiceGrid((1..5).toList(), columns = 3) { note -> Choice(s.feedbackNote == note, s.busy, note.toString(), "", "rating-$note", "$note de 5, ${listOf("muito ruim", "ruim", "regular", "bom", "muito bom")[note - 1]}") { action(AppAction.FeedbackNote(note)) } }
            OutlinedTextField(s.feedbackComment, { action(AppAction.FeedbackComment(it)) }, Modifier.fillMaxWidth(), label = { Text("Comentário opcional") }, maxLines = 4, enabled = !s.busy)
            Primary("Enviar avaliação", !s.busy && s.feedbackNote != null) { action(AppAction.Rate(false)) }
            Secondary("Pular avaliação", !s.busy) { action(AppAction.Rate(true)) }
        }
    }
}
