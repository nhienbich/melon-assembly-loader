# lib/mcs.dll

Mono.CSharp compiler dùng lúc chạy để biên dịch script. File lấy từ [BepInEx.ScriptLoader](https://github.com/ghorsington/BepInEx.ScriptLoader) (bản `mcs` đã sửa của [denikson/mcs-unity](https://github.com/denikson/mcs-unity)): cho phép phần lớn C# 7 và truy cập member private.

Copy `mcs.dll` vào `UserLibs/` của game. Đừng thả vào `Mods/` — MelonLoader sẽ cố load nó như một mod.

Game Mono profile .NET Standard (có `netstandard.dll`, không có `System.Reflection.Emit`) không compile được.
