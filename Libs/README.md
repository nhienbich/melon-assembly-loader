# Libs

Chỉ cần thư mục này nếu bạn **không** trỏ `GameDir` tới game.

Copy từ bản cài MelonLoader 0.6+ (sau khi mở game một lần):

| File | Nguồn |
| --- | --- |
| `MelonLoader.dll` | `MelonLoader/net6/MelonLoader.dll` |
| `Il2CppInterop.Runtime.dll` | `MelonLoader/net6/Il2CppInterop.Runtime.dll` |
| `Il2Cppmscorlib.dll` | `MelonLoader/Il2CppAssemblies/Il2Cppmscorlib.dll` |
| `UnityEngine.CoreModule.dll` | `MelonLoader/Il2CppAssemblies/` — tên có thể là `Il2CppUnityEngine.CoreModule.dll` |

`0Harmony.dll` (cùng thư mục `net6`) chỉ cần khi Payload tự patch.

DLL trong thư mục này bị gitignore.
