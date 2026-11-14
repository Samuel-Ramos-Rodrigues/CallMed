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
import androidx.compose.ui.text.font.Font
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.unit.Density
import androidx.compose.ui.unit.sp
import androidx.core.view.WindowCompat

private val CallMedFont = FontFamily(Font(R.font.manrope_regular, FontWeight.Normal), Font(R.font.manrope_medium, FontWeight.Medium), Font(R.font.manrope_semibold, FontWeight.SemiBold), Font(R.font.manrope_bold, FontWeight.Bold))

internal fun callMedColors(dark: Boolean, contrast: Boolean): ColorScheme = if (dark) darkColorScheme(
    primary = if (contrast) Color(0xFFD6D0FF) else Color(0xFFB9B2FF), onPrimary = Color(0xFF181332),
    primaryContainer = Color(0xFF2C2C53), onPrimaryContainer = Color(0xFFF0EDFF),
    secondary = Color(0xFFB1EDCF), onSecondary = Color(0xFF0F3027), secondaryContainer = Color(0xFF173D35), onSecondaryContainer = Color(0xFFB1EDCF),
    background = if (contrast) Color(0xFF030616) else Color(0xFF0D1426), onBackground = Color(0xFFF4F5FF),
    surface = if (contrast) Color(0xFF0C1327) else Color(0xFF162239), onSurface = Color(0xFFF4F5FF),
    surfaceVariant = Color(0xFF263653), onSurfaceVariant = if (contrast) Color(0xFFF1F3FF) else Color(0xFFB8C6E0),
    outline = if (contrast) Color(0xFFBAC8F0) else Color(0xFF7184A6), outlineVariant = if (contrast) Color(0xFFBAC8F0) else Color(0xFF7184A6),
    error = Color(0xFFFFBAC4), onError = Color(0xFF471421), errorContainer = Color(0xFF4B1D2C), onErrorContainer = Color(0xFFFFD9DF),
    surfaceContainer = Color(0xFF162239), surfaceContainerHigh = Color(0xFF263653), surfaceContainerHighest = Color(0xFF263653), surfaceContainerLow = Color(0xFF121D33)
) else lightColorScheme(
    primary = if (contrast) Color(0xFF302084) else Color(0xFF4D46C8), onPrimary = Color.White,
    primaryContainer = Color(0xFFEEEDFF), onPrimaryContainer = Color(0xFF24165C),
    secondary = Color(0xFF125340), onSecondary = Color.White, secondaryContainer = Color(0xFFD9F1E6), onSecondaryContainer = Color(0xFF125340),
    background = if (contrast) Color.White else Color(0xFFF4F6FC), onBackground = Color(0xFF182445),
    surface = Color.White, onSurface = Color(0xFF182445), surfaceVariant = Color(0xFFE9EDF8),
    onSurfaceVariant = if (contrast) Color(0xFF14223F) else Color(0xFF536380),
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
            headlineLarge = TextStyle(fontFamily = CallMedFont, fontSize = 28.sp, lineHeight = 36.sp, fontWeight = FontWeight.Bold, letterSpacing = (-0.7).sp),
            headlineMedium = TextStyle(fontFamily = CallMedFont, fontSize = 25.sp, lineHeight = 32.sp, fontWeight = FontWeight.SemiBold),
            titleLarge = TextStyle(fontFamily = CallMedFont, fontSize = 21.sp, lineHeight = 29.sp, fontWeight = FontWeight.SemiBold),
            titleMedium = TextStyle(fontFamily = CallMedFont, fontSize = 18.sp, lineHeight = 25.sp, fontWeight = FontWeight.SemiBold),
            titleSmall = TextStyle(fontFamily = CallMedFont, fontSize = 16.sp, lineHeight = 23.sp, fontWeight = FontWeight.SemiBold),
            bodyLarge = TextStyle(fontFamily = CallMedFont, fontSize = 16.sp, lineHeight = 24.sp), bodyMedium = TextStyle(fontFamily = CallMedFont, fontSize = 16.sp, lineHeight = 24.sp),
            bodySmall = TextStyle(fontFamily = CallMedFont, fontSize = 14.sp, lineHeight = 21.sp), labelLarge = TextStyle(fontFamily = CallMedFont, fontSize = 16.sp, lineHeight = 23.sp, fontWeight = FontWeight.SemiBold),
            labelMedium = TextStyle(fontFamily = CallMedFont, fontSize = 14.sp, lineHeight = 20.sp, fontWeight = FontWeight.SemiBold),
            labelSmall = TextStyle(fontFamily = CallMedFont, fontSize = 12.sp, lineHeight = 18.sp, fontWeight = FontWeight.SemiBold)
        ), content = content)
    }
}
internal val LocalReading = staticCompositionLocalOf { ReadingPreferences() }
