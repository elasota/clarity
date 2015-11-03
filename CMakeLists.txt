cmake_minimum_required (VERSION 2.8.5)
project (clarity)
include (GenerateExportHeader)

add_executable(TestApp
	ClarityCorLibExported/tTests/tTestInterfaceOverrideCollision/tMyBase.Code.cpp
	ClarityCorLibExported/tTests/tTestInterfaceOverrideCollision/tMyDerived.Code.cpp
	ClarityCorLibExported/tTests/tTestInterfaceOverrideCollision/tMyInterfaceA_t_1.Code.cpp
	ClarityCorLibExported/tTests/tTestInterfaceOverrideCollision/tMyInterfaceB.Code.cpp
	ClarityCorLibExported/tTests/tTestInterfaceOverrideCollision/tMyInterfaceC.Code.cpp
	ClarityCorLibExported/tTests/tTestInterfaceOverrideCollision.Code.cpp
	ClarityCorLibExported/tSystem/tObject.Code.cpp
)

target_include_directories(TestApp PUBLIC
	$<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/ClarityCore>
	$<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/ClarityCorLibExported>
)

install (TARGETS TestApp DESTINATION bin)
