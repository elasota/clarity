# Clarity
Clarity is a small .NET implementation intended for games and other programs that need a small but powerful scripting platform.

Games mostly rely on special-purpose languages or Lua for scripting, or other similar languages.  While Lua is simple, it doesn't support SMP, has a lot of runtime overhead, and doesn't support aggregate types (like math vectors) well.  Proprietary languages can be higher-performance than Lua, but generally lack features.

.NET is conceptually a great fit for games: It has good support for value types, is very fast thanks to its strong typing, is compiled from many high-quality languages with mature tools, and is safer than C++.

However, .NET also suffers from difficult portability due to the huge scope of its standard framework and the inability to JIT code on platforms with code signing.

Clarity is intended to bridge the gap and bring modern .NET features in a compact implementation.  Its framework is based on the .NET Micro Framework, with some additions.

The implementation is driven by a 2-step AOT compiler that targets a RISC-like interpreter or C++ and a runtime library that aims for maximum portability.
