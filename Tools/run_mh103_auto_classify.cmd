@echo off
cd /d C:\TeamProject\finalprojectServerSouls
"C:\Program Files\Blender Foundation\Blender 5.1\blender.exe" --background --factory-startup --python "C:\TeamProject\finalprojectServerSouls\Tools\auto_classify_fbx_actions.py" -- "C:\Users\user\Downloads\103.fbx" "C:\TeamProject\finalprojectServerSouls\Temp\mh103_auto_classification.csv" "C:\TeamProject\finalprojectServerSouls\Temp\mh103_auto_classification_kr.md" 8 > "C:\TeamProject\finalprojectServerSouls\Temp\mh103_auto_classify_stdout.log" 2> "C:\TeamProject\finalprojectServerSouls\Temp\mh103_auto_classify_stderr.log"
