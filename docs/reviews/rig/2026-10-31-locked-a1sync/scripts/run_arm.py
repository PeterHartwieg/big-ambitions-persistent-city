from pathlib import Path
import subprocess,sys,time,json
S=Path(__file__).parent
arm=sys.argv[1]
with (S/(arm+'-launch.private.log')).open('w') as log:
 launch=subprocess.Popen(['powershell','-NoProfile','-File',str(S/'Run-Arm.ps1'),arm],stdout=log,stderr=subprocess.STDOUT)
 try:
  result=subprocess.run([sys.executable,str(S/'sync_driver.py'),arm])
  if result.returncode:
   import rig
   try:rig.call(arm+'-error-quit','quit')
   except Exception as error:print('Cleanup quit request:',repr(error),flush=True)
  launch.wait()
  print('Launcher exit',arm,launch.returncode,flush=True)
  (S/(arm+'-launcher-exit.json')).write_text(json.dumps(dict(arm=arm,exitCode=launch.returncode,driverExitCode=result.returncode)))
  sys.exit(result.returncode or launch.returncode)
 except BaseException:
  import rig
  try:rig.call(arm+'-interrupted-quit','quit')
  except Exception:pass
  launch.wait()
  raise
