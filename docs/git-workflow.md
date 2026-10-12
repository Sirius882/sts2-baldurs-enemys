# Git workflow

This directory is the independent Baldurs Enemys source repository. Its `main` branch tracks `origin/main` at https://github.com/Sirius882/sts2-baldurs-enemys.git. Denia is maintained in a separate repository at `../denia`; the shared workspace root is not a Git repository.

From the workspace root:

```powershell
git -C myModresources/baldurs_enemys status
git -C myModresources/baldurs_enemys add <files>
git -C myModresources/baldurs_enemys commit -m "Describe the change"
git -C myModresources/baldurs_enemys push
```

Use the existing workspace `build_baldurs_enemys.bat` and `pack_baldurs_enemys.bat` scripts. Source resources and Godot import metadata stay tracked; generated caches and PCK files stay ignored. Temporary scripts, logs and caches belong only in the workspace-root `.cache/`, and that directory must be safe to delete. Do not recreate the workspace-root `fit_beta/` directory.
