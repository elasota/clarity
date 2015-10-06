#include <string.h>

#include "ClarityCore.h"
#include "tSystem\tObject.Def.h"


::CLRUtil::Ref<::CLRX::NtSystem::tString >::Type (::CLRX::NtSystem::tObject::mcode_tToString)(const ::CLRExec::Frame &frame)
{
    return ::CLRUtil::Ref<::CLRX::NtSystem::tString>::Null();
}

::CLRX::NtSystem::tBoolean (::CLRX::NtSystem::tObject::mcode_tEquals)(const ::CLRExec::Frame &frame, ::CLRUtil::Ref<::CLRX::NtSystem::tObject >::Type param0)
{
    ::CLRX::NtSystem::tBoolean b;
    memset(&b, 0, sizeof(b));
    return b;
}

::CLRX::NtSystem::tInt32 (::CLRX::NtSystem::tObject::mcode_tGetHashCode)(const ::CLRExec::Frame &frame)
{
    ::CLRX::NtSystem::tInt32 i;
    memset(&i, 0, sizeof(i));
    return i;
}

void (::CLRX::NtSystem::tObject::mcode_tFinalize)(const ::CLRExec::Frame &frame)
{
}

