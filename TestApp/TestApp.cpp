#include "..\ClarityCorLibExported\tTests\tTestInterfaceOverrideCollision.h"

int main(int argc, const char **argv)
{
    ::CLRCore::IObjectManager *objManager = CLARITY_NULLPTR;
    ::CLRX::NtTests::tTestInterfaceOverrideCollision test;
    ::CLRExec::RootLevelFrame frame(objManager);
    test.mcall_itRun(frame);
    return 0;
}
