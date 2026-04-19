# SingTray Installer

`setup.iss` installs the published desktop app into `C:\Program Files\SingTray` and keeps runtime data in `C:\ProgramData\SingTray`.

`publish.ps1` publishes `SingTray.Client` and `SingTray.Service` into separate temporary directories, then merges them into `Installer\staging` so the two publish outputs never overwrite each other.
