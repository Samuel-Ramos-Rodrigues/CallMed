# O AndroidViewModelFactory usa este construtor ao restaurar a Activity.
-keep class com.callmed.app.CallMedViewModel { public <init>(android.app.Application); }
# O SDK WebRTC resolve classes e callbacks via JNI. Preserve os nomes na versão release.
-keep class org.webrtc.** { *; }
-keep interface org.webrtc.** { *; }
