package com.callmed.app

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.enableEdgeToEdge
import androidx.activity.compose.setContent
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.compose.LocalLifecycleOwner
import androidx.lifecycle.repeatOnLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.compose.runtime.*
import kotlinx.coroutines.delay

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent {
            val vm: CallMedViewModel = viewModel()
            val state by vm.state.collectAsStateWithLifecycle()
            val lifecycle = LocalLifecycleOwner.current.lifecycle
            LaunchedEffect(lifecycle, state.screen, state.authenticated) {
                if (state.authenticated && state.screen == Screen.CHAT) {
                    lifecycle.repeatOnLifecycle(Lifecycle.State.STARTED) {
                        while (true) { delay(4500); vm.pollChat() }
                    }
                }
            }
            LaunchedEffect(lifecycle, state.screen, state.authenticated) { if (state.authenticated && state.screen == Screen.JOURNEY) lifecycle.repeatOnLifecycle(Lifecycle.State.STARTED) { while (true) { delay(5000); vm.pollJourney() } } }
            CallMedTheme(state.reading) { CallMedApp(state, vm.clinicalApi, vm::clinicalPreparation, vm::handle) }
        }
    }
}
