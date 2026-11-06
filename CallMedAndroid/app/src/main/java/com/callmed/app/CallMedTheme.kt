package com.callmed.app

import android.app.Activity
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.LocalView
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.Density
import androidx.compose.ui.unit.sp
import androidx.core.view.WindowCompat

internal fun callMedColors(dark: Boolean, contrast: Boolean): ColorScheme = if (dark) darkColorScheme(
    primary = if (contrast) Color(0xFFD6D0FF) else Color(0xFFB1A9FF), onPrimary = Color(0xFF181332),
    primaryContainer = Color(0xFF292548), onPrimaryContainer = Color(0xFFF0EDFF),
    secondary = Color(0xFFB1EDCF), onSecondary = Color(0xFF0F3027), secondaryContainer = Color(0xFF173D35), onSecondaryContainer = Color(0xFFB1EDCF),
    background = if (contrast) Color(0xFF030616) else Color(0xFF0C1124), onBackground = Color(0xFFF4F5FF),
    surface = if (contrast) Color(0xFF0C1327) else Color(0xFF161F36), onSurface = Color(0xFFF4F5FF),
    surfaceVariant = Color(0xFF202B46), onSurfaceVariant = if (contrast) Color(0xFFF1F3FF) else Color(0xFFC0CBE3),
    outline = if (contrast) Color(0xFFBAC8F0) else Color(0xFF657595), outlineVariant = if (contrast) Color(0xFFBAC8F0) else Color(0xFF657595),
    error = Color(0xFFFFBAC4), onError = Color(0xFF471421), errorContainer = Color(0xFF4B1D2C), onErrorContainer = Color(0xFFFFD9DF),
    surfaceContainer = Color(0xFF161F36), surfaceContainerHigh = Color(0xFF202B46), surfaceContainerHighest = Color(0xFF202B46), surfaceContainerLow = Color(0xFF111A30)
) else lightColorScheme(
    primary = if (contrast) Color(0xFF302084) else Color(0xFF5143C3), onPrimary = Color.White,
    primaryContainer = Color(0xFFEAE6FF), onPrimaryContainer = Color(0xFF24165C),
    secondary = Color(0xFF125340), onSecondary = Color.White, secondaryContainer = Color(0xFFD9F1E6), onSecondaryContainer = Color(0xFF125340),
    background = if (contrast) Color.White else Color(0xFFF3F5FC), onBackground = Color(0xFF17213A),
    surface = Color.White, onSurface = Color(0xFF17213A), surfaceVariant = Color(0xFFE9EDF8),
    onSurfaceVariant = if (contrast) Color(0xFF14223F) else Color(0xFF43516C),
    outline = if (contrast) Color(0xFF223557) else Color(0xFF7C89A3), outlineVariant = if (contrast) Color(0xFF223557) else Color(0xFF7C89A3),
    error = Color(0xFFA01C37), onError = Color.White, errorContainer = Color(0xFFFFE0E5), onErrorContainer = Color(0xFF6B0B22),
    surfaceContainer = Color.White, surfaceContainerHigh = Color(0xFFE9EDF8), surfaceContainerHighest = Color(0xFFE9EDF8), surfaceContainerLow = Color(0xFFF7F8FF)
)

@Composable
internal fun CallMedTheme(reading: ReadingPreferences, content: @Composable () -> Unit) {
    val dark = reading.theme == "dark" || (reading.theme == "system" && isSystemInDarkTheme())
    val density = LocalDensity.current
    val view = LocalView.current
    SideEffect {
        (view.context as? Activity)?.window?.let { window ->
            WindowCompat.getInsetsController(window, view).apply {
                isAppearanceLightStatusBars = !dark
                isAppearanceLightNavigationBars = !dark
            }
        }
    }
    // Nunca reduz o tamanho configurado pelo usuário no Android.
    val scaled = Density(density.density, maxOf(density.fontScale, reading.scale))
    CompositionLocalProvider(LocalDensity provides scaled, LocalReading provides reading) {
        MaterialTheme(colorScheme = callMedColors(dark, reading.contrast), typography = Typography(
            headlineLarge = TextStyle(fontSize = 28.sp, lineHeight = 35.sp, fontWeight = FontWeight.Medium),
            headlineMedium = TextStyle(fontSize = 25.sp, lineHeight = 32.sp, fontWeight = FontWeight.Medium),
            titleLarge = TextStyle(fontSize = 21.sp, lineHeight = 29.sp, fontWeight = FontWeight.Medium),
            titleMedium = TextStyle(fontSize = 18.sp, lineHeight = 25.sp, fontWeight = FontWeight.Medium),
            titleSmall = TextStyle(fontSize = 16.sp, lineHeight = 23.sp, fontWeight = FontWeight.Medium),
            bodyLarge = TextStyle(fontSize = 16.sp, lineHeight = 24.sp), bodyMedium = TextStyle(fontSize = 16.sp, lineHeight = 24.sp),
            bodySmall = TextStyle(fontSize = 14.sp, lineHeight = 21.sp), labelLarge = TextStyle(fontSize = 16.sp, lineHeight = 23.sp, fontWeight = FontWeight.Medium),
            labelMedium = TextStyle(fontSize = 14.sp, lineHeight = 20.sp, fontWeight = FontWeight.Medium),
            labelSmall = TextStyle(fontSize = 12.sp, lineHeight = 18.sp, fontWeight = FontWeight.Medium)
        ), content = content)
    }
}
internal val LocalReading = staticCompositionLocalOf { ReadingPreferences() }
