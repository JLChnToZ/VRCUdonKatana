# VRC Udon Katana
This is an experimental compiler that compiles [Katana](https://github.com/JLChnToZ/Katana) to VRChat UDON Assembly. This is a minimal program langauge (or tree data structure expression) that I have created few years ago, and I had modified a bit to make it compatible with UDON's environment. This project is not aim to compete with existing tools such as U# or UdonPie, but a just-for-fun project focus on deep diving into UDON internals.

In this project it contains these components:
- **Katana Expressions Parser**:
  This parser is copied from my previous project and sightly modified for supporting more type literals.
- **Udon Katana Compiler**:
  Internal stuff, glues the Katana Parser and UDON Assembly.
- **Udon Assembly Builder**: 
  Use for programmitaclly generate UDON Assembly, influenced by System.Reflection.Emit.ILGenerator, can be used standalone.

For how to program with this language, I will document it once I have time to do so, but you can have a glance on how it looks here:
```
(
  ; This is the simple script that can let user click to teleport to descied position.
  ; < This is a comment marker, btw.
  var(target, public, Transform, ), ; Declare a target transform variable

  when(_start, ( ; `when(..., (..., ...))` is an event entry point
    ; Called on initialize
    DebugLog(Udon Katana works!) ; String literals can be unquoted unless it contains commas or parentheses, or it is numeric.
  )),

  when(_interact, (
    ; Called on player interact
    TeleportTo((GetNetworkingLocalPlayer), GetPosition($(target)), GetRotation($(target))),
    ; `(...)` is a zero-parameter method call because `xxx()` is as same as `xxx` in Katana's world.
    ; But if it contains comma, it will become code blocks (same as C-style language's large brackets).
    ; `$(...)` is a local variable getter.
    ; According to the UDON method conversion rules, `GetPosition(...)` is equivalent on Unity C#'s transform.position,
    ; details will be added soon.
  )),

  ; The Katana language itself has defined flow control keywords such as `if`, `while`, `=`, etc.
  ; Just remember, Katana defines commands before parentheses, never at the middle or the end.
)
```

## Getting Started
The project depends on VRChat SDK3 for Worlds, so please setup these after cloneing:
- [Unity Editor (2019.4.31f1)](https://unity3d.com/unity/whats-new/2019.4.31)
- [The latest VRChat SDK3 for worlds](https://vrchat.com/home/download)

To be done.

## License
The code are licensed with [MIT License](LICENSE).

It is not required to credit me when you used this asset, but I will be happy if you did it.
