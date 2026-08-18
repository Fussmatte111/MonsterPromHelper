@echo off
cd /d "%~dp0"
python -m pip install -q -r requirements.txt
python ..\tools\build_event_db.py
python -m src.main --overlay
