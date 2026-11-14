@file:OptIn(androidx.compose.foundation.layout.ExperimentalLayoutApi::class)
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
import androidx.compose.material.icons.outlined.History
import androidx.compose.material.icons.outlined.MoreVert
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.semantics.*
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.buildAnnotatedString
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.withStyle
import androidx.compose.ui.text.input.KeyboardCapitalization
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.unit.dp

@Composable internal fun ChatScreen(s: UiState, action: (AppAction) -> Unit) {
    val list = rememberLazyListState()
    var options by remember { mutableStateOf(false) }
    val reduce = s.reading.reduceMotion
    LaunchedEffect(s.chat.messages.lastOrNull()?.id, s.chat.avaliacaoId, s.feedbackAccepted, s.sendingChat) {
        if (list.layoutInfo.totalItemsCount > 0) {
            val last = list.layoutInfo.totalItemsCount - 1
            if (reduce) list.scrollToItem(last) else list.animateScrollToItem(last)
        }
    }
    Column(Modifier.fillMaxSize().imePadding()) {
        Row(Modifier.fillMaxWidth().padding(horizontal = 18.dp, vertical = 7.dp), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
            Text(if (s.chat.modo == "Humano") "Equipe" else "Chat", Modifier.weight(1f), style = MaterialTheme.typography.titleSmall)
            if (s.chat.hasPrevious) {
                if (LocalDensity.current.fontScale >= 1.5f) IconButton(onClick = { action(AppAction.PreviousChats()) }, Modifier.size(48.dp), enabled = !s.busy) { Icon(Icons.Outlined.History, "Atendimentos anteriores") }
                else TextButton(onClick = { action(AppAction.PreviousChats()) }, Modifier.heightIn(min = 48.dp), enabled = !s.busy) { Icon(Icons.Outlined.History, null); Spacer(Modifier.width(6.dp)); Text("Anteriores") }
            }
            Box {
                IconButton(onClick = { options = true }, Modifier.size(48.dp), enabled = !s.busy) { Icon(Icons.Outlined.MoreVert, "Opções do atendimento") }
                DropdownMenu(options, onDismissRequest = { options = false }) {
                    DropdownMenuItem(text = { Text("Novo atendimento") }, onClick = { options = false; action(AppAction.NewChat) })
                    if (!s.chat.encerrada && s.chat.messages.isNotEmpty()) DropdownMenuItem(text = { Text("Encerrar atendimento") }, onClick = { options = false; action(AppAction.EndChat) })
                }
            }
        }
        LazyColumn(Modifier.weight(1f).fillMaxWidth().testTag("chat-history"), state = list, contentPadding = PaddingValues(horizontal = 18.dp, vertical = 14.dp), verticalArrangement = Arrangement.spacedBy(if (s.reading.spacious) 22.dp else 14.dp)) {
            if (s.chat.messages.isEmpty() && !s.chat.encerrada) item { Panel { SectionTitle("Como podemos ajudar?"); Muted("Envie sua dúvida sobre consultas, horários ou atendimento. Você também pode pedir para falar com a equipe.") } }
            if (s.chat.hasMore) item { Secondary("Mensagens anteriores deste atendimento", !s.busy) { action(AppAction.OlderChatMessages) } }
            if (!s.chat.encerrada) items(s.chat.messages, key = { it.id }) { message -> ChatBubble(message, message.id == s.chat.messages.lastOrNull()?.id) }
            if (s.busy) item { Row(Modifier.padding(10.dp).semantics { liveRegion = LiveRegionMode.Polite }, verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(10.dp)) { CircularProgressIndicator(Modifier.size(20.dp), strokeWidth = 2.dp); Text(if (s.sendingChat) "Preparando a resposta…" else "Atualizando atendimento…", style = MaterialTheme.typography.bodySmall) } }
            if (!s.sendingChat) {
                if (s.chat.encerrada) item { Panel { SectionTitle("Atendimento finalizado"); Muted("Esta conversa ficou salva em Anteriores. Quando precisar, comece um novo atendimento."); Primary("Iniciar novo atendimento", !s.busy) { action(AppAction.NewChat) } } }
                if (s.chat.encerrada && s.chat.avaliacaoId != null) item { Feedback(s, action) }
            }
        }
        Surface(color = MaterialTheme.colorScheme.surface) {
            Row(Modifier.fillMaxWidth().padding(horizontal = 14.dp, vertical = 10.dp).testTag("chat-composer"), verticalAlignment = Alignment.Bottom, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                OutlinedTextField(s.chatDraft, { action(AppAction.ChatDraft(it)) }, Modifier.weight(1f).testTag("chat-draft").semantics { contentDescription = "Sua mensagem" }, placeholder = { Text("Digite sua mensagem…", style = MaterialTheme.typography.bodySmall) }, maxLines = 4, enabled = !s.busy,
                    shape = RoundedCornerShape(18.dp),
                    keyboardOptions = KeyboardOptions(capitalization = KeyboardCapitalization.Sentences))
                FilledIconButton(onClick = { action(AppAction.Send) }, Modifier.size(56.dp).testTag("chat-send"), enabled = !s.busy && s.chatDraft.isNotBlank(),
                    colors = IconButtonDefaults.filledIconButtonColors(disabledContainerColor = Color.Transparent)) {
                    Icon(Icons.AutoMirrored.Outlined.Send, "Enviar mensagem")
                }
            }
        }
    }
}
@Composable private fun ChatBubble(message: ChatMessage, latest: Boolean) {
    val own = message.autor == "paciente"
    BoxWithConstraints(Modifier.fillMaxWidth()) {
    val bubbleWidth = maxWidth * .92f
    Row(Modifier.fillMaxWidth(), horizontalArrangement = if (own) Arrangement.End else Arrangement.Start) {
        Surface(Modifier.widthIn(max = bubbleWidth).then(if (latest && !own) Modifier.semantics { liveRegion = LiveRegionMode.Polite } else Modifier),
            color = if (own) MaterialTheme.colorScheme.primaryContainer else MaterialTheme.colorScheme.surface, shape = RoundedCornerShape(17.dp), border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant)) {
            Column(Modifier.padding(12.dp), verticalArrangement = Arrangement.spacedBy(5.dp)) {
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
}
@Composable internal fun PreviousChatsScreen(s: UiState, action: (AppAction) -> Unit) {
    val archive = s.archive
    if (s.screen == Screen.CHAT_ARCHIVE) {
        Page {
            Back(!s.busy) { action(AppAction.Back) }
            PageTitle("SEU HISTÓRICO", "Atendimentos anteriores", "Abra uma conversa para consultar as mensagens. O atendimento atual continua separado.")
            if (archive.items.isEmpty()) Muted("Você ainda não tem atendimentos finalizados.")
            archive.items.forEach { chat ->
                Panel {
                    SectionTitle(archiveDate(chat.endedAt))
                    Text(chat.subject)
                    Secondary("Ver conversa", !s.busy) { action(AppAction.ReadPrevious(chat)) }
                }
            }
            if (archive.nextCursor != null) Secondary("Mais atendimentos", !s.busy) { action(AppAction.PreviousChats(true)) }
        }
    } else {
        LazyColumn(Modifier.fillMaxSize().testTag("previous-chat-history"), contentPadding = PaddingValues(18.dp), verticalArrangement = Arrangement.spacedBy(14.dp)) {
            item { Back(!s.busy) { action(AppAction.Back) }; SectionTitle("Conversa anterior"); Muted(archiveDate(archive.selected?.endedAt.orEmpty())); Muted("Somente leitura. Para conversar, volte ao atendimento atual.") }
            if (archive.hasMore) item { Secondary("Carregar mensagens anteriores", !s.busy) { action(AppAction.OlderPreviousMessages) } }
            if (archive.messages.isEmpty()) item { Muted("As mensagens deste atendimento não estão mais disponíveis.") }
            items(archive.messages, key = { it.id }) { ChatBubble(it, false) }
        }
    }
}
private fun archiveDate(value: String) = runCatching {
    val instant = runCatching { java.time.OffsetDateTime.parse(value).toInstant() }.getOrElse {
        java.time.LocalDateTime.parse(value).toInstant(java.time.ZoneOffset.UTC)
    }
    instant.atZone(java.time.ZoneId.systemDefault()).format(java.time.format.DateTimeFormatter.ofPattern("dd/MM/yyyy · HH:mm"))
}.getOrDefault(value.take(16).replace('T', ' '))
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
