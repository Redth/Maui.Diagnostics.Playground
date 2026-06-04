cmake_minimum_required(VERSION 3.22.1)

project(crashnativekit LANGUAGES CXX)

add_library(crashnativekit SHARED crash_native_kit.cpp)

target_compile_features(crashnativekit PRIVATE cxx_std_17)
target_compile_options(crashnativekit PRIVATE
    -fvisibility=hidden
    -fno-omit-frame-pointer
    -fno-optimize-sibling-calls)
target_link_options(crashnativekit PRIVATE
    -Wl,--build-id=sha1
    -Wl,-z,max-page-size=16384)
