plugins {
    id("com.android.library")
}

android {
    namespace = "dev.redth.maui.diagnostics.playground.nativeandroid"
    compileSdk = 36

    defaultConfig {
        minSdk = 24

        externalNativeBuild {
            cmake {
                cppFlags += listOf(
                    "-std=c++17",
                    "-fno-omit-frame-pointer",
                    "-fno-optimize-sibling-calls"
                )
            }
        }

        ndk {
            abiFilters += listOf("arm64-v8a", "armeabi-v7a", "x86", "x86_64")
        }
    }

    externalNativeBuild {
        cmake {
            path = file("src/main/cpp/CMakeLists.txt")
        }
    }

    packaging {
        jniLibs.keepDebugSymbols += listOf("**/libcrashnativekit.so")
    }
}
