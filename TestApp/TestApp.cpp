#include "tSystem\tObject.Def.h"
#include "..\ClarityCorLibExported\tTests\tTestInterfaceOverrideCollision.Def.h"

int main(int argc, const char **argv)
{
    ::CLRX::NtTests::tTestInterfaceOverrideCollision test;
    ::CLRExec::Frame frame;
    test.mcall_tRun(frame);
    return 0;
}
