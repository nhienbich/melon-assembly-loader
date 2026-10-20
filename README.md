# AssemblyLoader cho MelonLoader (IL2CPP)

Mod [MelonLoader](https://github.com/LavaGang/MelonLoader) 0.6+ để nạp và chạy một assembly C# **khi game đang mở**. Viết cho game Unity IL2CPP, để debug bản build release.

Port từ [BepInEx.IL2CPP.AssemblyLoader](https://github.com/zongjingyao/BepInEx.IL2CPP.AssemblyLoader). Hợp đồng giữ nguyên: `public static void Main()` khi nạp, `public static void Unload()` trước khi nạp lại.

## Cách dùng

### 1. MelonLoader

Cài MelonLoader **0.6 trở lên** cho game IL2CPP (cần .NET 6 Desktop Runtime). Mở game một lần để generator tạo `MelonLoader/Il2CppAssemblies`.

Bản này không chạy trên game Mono. Mono của MelonLoader không có collectible `AssemblyLoadContext`.

### 2. Trỏ thư viện

Sửa `GameDir` trong [Directory.Build.props](Directory.Build.props) thành thư mục game:

```xml
<GameDir>D:\Games\YourGame</GameDir>
```

Hoặc copy DLL vào `Libs/` (xem [Libs/README.md](Libs/README.md)).

### 3. Cài mod

```bash
dotnet build MelonLoader.IL2CPP.AssemblyLoader.sln -c Release
```

Copy `AssemblyLoader/bin/Release/net6.0/AssemblyLoader.dll` vào `Mods/`.

Muốn copy tự động thì bỏ comment target `CopyToMods` trong `AssemblyLoader.csproj`.

### 4. Viết payload

Sửa [Payload/Payload.cs](Payload/Payload.cs), build project `Payload`, copy:

- `Payload.dll` → `UserData/assembly/Payload.dll`
- `Payload.pdb` → cùng thư mục (tuỳ chọn, để stack trace đúng dòng)

Tạo thư mục `UserData/assembly` nếu chưa có. Mod cũng tự tạo lúc game chạy.

Lần mở game sau, mod thấy DLL có sẵn và gọi `Main()`. Mỗi lần file đổi (save từ IDE), mod đợi ~600ms rồi gọi `Unload()` trên bản cũ và `Main()` trên bản mới. Xoá file thì chỉ gọi `Unload()`.

Bỏ comment target `CopyToAssemblyFolder` trong `Payload.csproj` nếu muốn build xong là copy vào game.

## Hợp đồng

Trong bất kỳ class nào, **method public static đầu tiên** đúng tên sẽ được gọi:

```csharp
public static void Main()
{
    MelonLogger.Msg("Payload.Main invoked.");
}

public static void Unload()
{
    // gỡ patch, Destroy object, xoá static
}
```

Không có tham số. Log bằng `MelonLogger`, đừng dùng `UnityEngine.Debug` — trên IL2CPP nó hay ném `AccessViolationException` (đúng lỗi mà bản BepInEx tránh bằng `ManualLogSource`).

`Unload()` bắt buộc nếu bạn muốn nạp lại sạch:

- `harmony.UnpatchSelf()` rồi gán `harmony = null`
- `Object.Destroy` mọi object / component bạn tạo
- không giữ delegate, event, coroutine trỏ vào code payload
- không cất type của payload vào static của MelonLoader hay Unity

Harmony cần reference `MelonLoader/net6/0Harmony.dll` (`<Private>false</Private>`).

Class kế thừa type Il2Cpp phải có constructor `IntPtr` và `ClassInjector.RegisterTypeInIl2Cpp` **trước** khi dùng. Type đã inject thì không gỡ được — đừng hot-reload những class đó.

## Khác bản BepInEx

| BepInEx 6 | Bản này |
| --- | --- |
| `BepInEx/plugins` + `BasePlugin` | `Mods/` + `MelonMod` |
| `BepInEx/assembly/Payload.dll` | `UserData/assembly/Payload.dll` |
| `Main(ManualLogSource)` | `Main()` + `MelonLogger` |
| `Assembly.Load` — DLL cũ nằm lại trong process | `AssemblyLoadContext` collectible, đọc bằng byte nên không khoá file |
| Watcher gọi thẳng từ threadpool | Xếp hàng, chạy trong `OnUpdate` (luồng Unity) |
| File có sẵn lúc mở game không được nạp | Có nạp |
| MD5 qua `Il2CppSystem.Security.Cryptography` | `System.Security.Cryptography.MD5` |

Debounce, thử lại khi file đang bị lock, và bỏ qua buffer chưa đủ header `MZ` là để Rider/Visual Studio ghi DLL theo nhiều nhịp không làm nát lần load.

## Layout

```text
MelonLoader.IL2CPP.AssemblyLoader/
  AssemblyLoader/     mod, copy DLL vào Mods/
  Payload/            code bạn sửa, copy DLL vào UserData/assembly/
  Libs/               DLL tham chiếu nếu không trỏ GameDir
  Directory.Build.props
```

Trong game:

```text
Mods/AssemblyLoader.dll
UserData/assembly/Payload.dll
UserData/assembly/Payload.pdb
```

## English

MelonLoader 0.6+ IL2CPP mod that hot-loads `UserData/assembly/Payload.dll` without restarting the game. Port of [BepInEx.IL2CPP.AssemblyLoader](https://github.com/zongjingyao/BepInEx.IL2CPP.AssemblyLoader).

1. Install MelonLoader 0.6+ on an IL2CPP game and launch once.
2. Set `GameDir` in `Directory.Build.props` to the game folder.
3. Build. Put `AssemblyLoader.dll` in `Mods/`.
4. Put `Payload.dll` in `UserData/assembly/`. The mod calls `public static void Main()` and, on change, `Unload()` then `Main()` again.

`Unload()` must drop Harmony patches, Unity objects and statics or the collectible load context will stay alive. Do not call `UnityEngine.Debug` (IL2CPP access violation). Mono games are not supported.

##Big Thank Grok AI.
