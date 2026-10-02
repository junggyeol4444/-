# -*- mode: python ; coding: utf-8 -*-
from PyInstaller.utils.hooks import collect_data_files

a = Analysis(['main.py'], pathex=[], binaries=[], datas=collect_data_files('tkinter'), hiddenimports=['tkinter','tkinter.ttk','tkinter.filedialog','tkinter.messagebox'], hookspath=[], hooksconfig={}, runtime_hooks=[], excludes=[])
pyz = PYZ(a.pure)
exe = EXE(pyz, a.scripts, a.binaries, a.datas, [], name='MYVOCAL-Studio', debug=False, bootloader_ignore_signals=False, strip=False, upx=True, console=False, icon=None)
