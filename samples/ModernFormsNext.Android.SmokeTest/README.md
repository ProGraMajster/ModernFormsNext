# ModernFormsNext Android smoke test

This is a technical native Android host for the WindowKit Android platform foundation. It is not
the default ModernFormsNext template and does not claim that framework controls render on Android.

Build it with:

```powershell
dotnet build .\samples\ModernFormsNext.Android.SmokeTest\ModernFormsNext.Android.SmokeTest.csproj
```

Run it from Visual Studio or deploy the generated debug APK to an API 23+ emulator/device. Verify:

1. camera starts as `Denied`, shows one serialized runtime dialog, and updates after denial/grant;
2. microphone reports `NotDeclared` and never opens a dialog;
3. notifications require a dialog on API 33+ and are logically granted on older versions;
4. a second denial with **Don't ask again** becomes `PermanentlyDenied`;
5. **Open App Settings** navigates only after the explicit button press;
6. rotation, background/foreground, and return from settings update Activity/lifecycle information;
7. the final merged manifest contains camera, notifications, and optional camera feature declarations,
   but does not contain microphone permission.

The activity forwards `OnRequestPermissionsResult` to `AndroidWindowKit`. This keeps the backend free
of AndroidX/MAUI dependencies while preserving one central request coordinator.
