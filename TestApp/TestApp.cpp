#include "../ClarityCorLibExported/tTests/tTestInterfaceOverrideCollision.h"
#include "../ClarityCorLibExported/tTests/tTestApi.h"


int main(int argc, const char **argv)
{
    ::CLRCore::IObjectManager *objManager = ::CLRCore::CreateObjectManager();
	::CLRExec::RootLevelFrame frame(objManager);
	
	::CLRX::NtTests::tTestInterfaceOverrideCollision *test = objManager->AllocObject< ::CLRX::NtTests::tTestInterfaceOverrideCollision >(frame);
    test->mcall_itRun(frame);
    return 0;
}

void ::CLRX::NtTests::tTestApi::mcode_stWriteLine(const ::CLRExec::Frame &frame, ::CLRVM::TValValue< ::CLRX::NtSystem::tString >::Type param0)
{
}
