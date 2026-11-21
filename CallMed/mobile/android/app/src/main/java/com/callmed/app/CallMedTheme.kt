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
    primary = if (contrast) Color(0xFFDCE8FF) else Color(0xFFB4CCFF), onPrimary = Color(0xFF102A55),
    primaryContainer = Color(0xFF20375C), onPrimaryContainer = Color(0xFFE2ECFF),
    secondary = Color(0xFF8EDBC9), onSecondary = Color(0xFF063B30), secondaryContainer = Color(0xFF173C37), onSecondaryContainer = Color(0xFF8EDBC9),
    background = if (contrast) Color(0xFF030914) else Color(0xFF0B1324), onBackground = Color(0xFFF0F4FC),
    surface = if (contrast) Color(0xFF0C172A) else Color(0xFF132038), onSurface = Color(0xFFF0F4FC),
    surfaceVariant = Color(0xFF21324C), onSurfaceVariant = if (contrast) Color(0xFFF2F6FF) else Color(0xFFB3C2DA),
    outline = if (contrast) Color(0xFFD1DCF0) else Color(0xFF8498B8), outlineVariant = if (contrast) Color(0xFFD1DCF0) else Color(0xFF2C3D58),
    error = Color(0xFFFFBAC4), onError = Color(0xFF471421), errorContainer = Color(0xFF4B1D2C), onErrorContainer = Color(0xFFFFD9DF),
    surfaceContainer = Color(0xFF132038), surfaceContainerHigh = Color(0xFF21324C), surfaceContainerHighest = Color(0xFF21324C), surfaceContainerLow = Color(0xFF101C30)
) else lightColorScheme(
    primary = if (contrast) Color(0xFF173574) else Color(0xFF294EA6), onPrimary = Color.White,
    primaryContainer = Color(0xFFE9F0FF), onPrimaryContainer = Color(0xFF18366D),
    secondary = Color(0xFF146450), onSecondary = Color.White, secondaryContainer = Color(0xFFE0F4EB), onSecondaryContainer = Color(0xFF146450),
    background = if (contrast) Color.White else Color(0xFFF5F7FB), onBackground = Color(0xFF182A43),
    surface = Color.White, onSurface = Color(0xFF182A43), surfaceVariant = Color(0xFFECF1F8),
    onSurfaceVariant = if (contrast) Color(0xFF14223F) else Color(0xFF536780),
    outline = if (contrast) Color(0xFF223557) else Color(0xFF73859E), outlineVariant = if (contrast) Color(0xFF223557) else Color(0xFFDCE4F0),
    error = Color(0xFFA01C37), onError = Color.White, errorContainer = Color(0xFFFFE0E5), onErrorContainer = Color(0xFF6B0B22),
    surfaceContainer = Color.White, surfaceContainerHigh = Color(0xFFECF1F8), surfaceContainerHighest = Color(0xFFECF1F8), surfaceContainerLow = Color(0xFFF8FAFD)
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
            headlineMedium = TextStyle(fontFamily = CallMedFont, fontSize = 25.sp, lineHeight = 33.sp, fontWeight = FontWeight.SemiBold),
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
