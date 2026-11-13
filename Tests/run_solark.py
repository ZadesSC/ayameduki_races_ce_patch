import os,sys,json,hashlib,subprocess,signal,time,shutil,argparse
from pathlib import Path
import xml.etree.ElementTree as ET
from rwtf.core import Lease
parser=argparse.ArgumentParser(description="Bounded mixed-version Solark runtime regression; requires installed rwtf")
parser.add_argument('--output',type=Path,required=True)
parser.add_argument('--game-dir',type=Path,required=True)
parser.add_argument('--launcher',type=Path,required=True)
parser.add_argument('--workshop',type=Path,required=True)
parser.add_argument('--bridge',type=Path,required=True,help='1.7 rwtf build output containing Assemblies')
parser.add_argument('--ability',action='store_true')
args=parser.parse_args()
for value in [args.output,args.game_dir,args.launcher,args.workshop,args.bridge]:
 if not value.is_absolute():parser.error('all paths must be absolute')
repo=Path(__file__).resolve().parents[1]
game=args.game_dir;steam=args.workshop;out=args.output
for protected in [repo,game,steam,args.bridge,args.launcher.parent]:
 if out.resolve().is_relative_to(protected.resolve()) or protected.resolve().is_relative_to(out.resolve()):
  parser.error('output must be separate from source, game, dependencies, launcher and bridge')
out.mkdir(parents=True,exist_ok=False)
base=out
profile=out/'profile';(profile/'Config').mkdir(parents=True)
def xml(path,root,values):
 r=ET.Element(root)
 for k,v in values.items():
  e=ET.SubElement(r,k)
  if isinstance(v,list):
   for x in v:ET.SubElement(e,'li').text=x
  else:e.text=str(v)
 ET.ElementTree(r).write(path,encoding='utf-8',xml_declaration=True)
# Separate identity avoids shadowing the existing Workshop addon registration.
# All runtime directories link to the exact repository; only About is a wrapper.
candidate=base/'candidate'
if not candidate.exists():
 (candidate/'About').mkdir(parents=True)
 doc=ET.parse(repo/'About/About.xml');doc.find('packageId').text='local.solark.candidate';doc.write(candidate/'About/About.xml',encoding='utf-8')
 for name in ['Patches','Defs','Assemblies','Textures']:(candidate/name).symlink_to(repo/name,target_is_directory=True)
test=base/'test-mod';(test/'Assemblies').mkdir(parents=True,exist_ok=True);(test/'About').mkdir(exist_ok=True)
xml(test/'About/About.xml','ModMetaData',{'name':'Solark isolated regression','packageId':'local.solark.regression','supportedVersions':['1.6','1.7']})
for name in ['Rwtf.Core.dll','Rwtf.Extension.dll']:shutil.copyfile(args.bridge/'Assemblies'/name,test/'Assemblies'/name)
mods=['ludeon.rimworld','brrainz.harmony','erdelf.humanoidalienraces','ayameduki.ayaracepremise','ayameduki.harsolark','ayameduki.harsolarkex','ceteam.combatextended','local.solark.candidate','local.solark.regression']
xml(profile/'Config/ModsConfig.xml','ModsConfigData',{'version':'1.7.4985','activeMods':mods,'knownExpansions':[]})
xml(profile/'Config/Prefs.xml','PrefsData',{'fullscreen':'False','screenWidth':1280,'screenHeight':800,'runInBackground':'True','devMode':'True','pauseOnError':'False','pauseOnLoad':'False','openLogOnWarnings':'False','volumeGame':0,'volumeMusic':0})
moddir=game/'Mods';links={moddir/('Solark-owned-candidate-'+str(os.getpid())):candidate,moddir/('Solark-owned-regression-'+str(os.getpid())):test}
roots={'addon':repo,'CE':steam/'2890901044','Solark':steam/'2608237489','Aya':steam/'3505571618','HAR':steam/'839005762','SolarkEX':steam/'3223847402'}
# Record the actual Harmony registration (the host may have a local 1.7 copy).
for entry in moddir.iterdir():
 try:
  if ET.parse(entry/'About/About.xml').findtext('packageId','').lower()=='brrainz.harmony':
   roots['Harmony-'+entry.name]=entry.resolve()
 except (OSError,ET.ParseError):pass

def identities():
 result={'test-source':{str(p.relative_to(repo)):hashlib.sha256(p.read_bytes()).hexdigest() for p in (repo/'Tests').rglob('*') if p.is_file() and p.suffix in ['.cs','.py']},'game':{str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in [game/'Version.txt',game/'RimWorld.app/Contents/MacOS/RimWorld',game/'RimWorld.app/Contents/MacOS/Assembly-CSharp.dll'] if p.is_file()}}
 for label,root in roots.items():
  result[label]={str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest() for p in root.rglob('*') if p.is_file() and p.suffix.lower() in ['.xml','.dll'] and '.git' not in p.parts and 'Source' not in p.parts and 'Tests' not in p.parts}
 return result
def inventory():return {x.name:os.readlink(x) if x.is_symlink() else 'directory' if x.is_dir() else 'file' for x in moddir.iterdir()}
with Lease() as lease:
 before=identities();entries=inventory();(out/'inputs-before.json').write_text(json.dumps(before,indent=2));(out/'mods-before.json').write_text(json.dumps(entries,indent=2))
 p=None;created=[]
 try:
  for link,target in links.items():
   if link.exists() or link.is_symlink():raise RuntimeError('Owned name already exists: '+str(link))
   link.symlink_to(target,target_is_directory=True);created.append((link,target))
  env={k:v for k,v in os.environ.items() if not k.lower().startswith(('steam','rwtf_','solark_test_'))};env.update(SOLARK_TEST_SOURCE=str(repo),SOLARK_TEST_SOLARK=str(steam/'2608237489'),SOLARK_TEST_OUTPUT=str(out),SOLARK_TEST_ABILITY='1' if args.ability else '0')
  cmd=[sys.executable,str(args.launcher),'--game-version','1.7','-savedatafolder='+str(profile),'-quicktest','-logFile',str(out/'game.log'),'-screen-fullscreen','0']
  (out/'command.json').write_text(json.dumps({'argv':cmd,'mods':mods,'ability':env['SOLARK_TEST_ABILITY'],'timeoutSeconds':240,'framework':'public Scenario and Context via diagnostic GameComponent; standard Runner rejects unsupported CE metadata/startup errors'},indent=2))
  with (out/'launcher.log').open('w') as log:
   p=subprocess.Popen(cmd,env=env,stdout=log,stderr=subprocess.STDOUT,start_new_session=True,pass_fds=(lease.file.fileno(),))
   (out/'pid.json').write_text(json.dumps({'pid':p.pid,'argv':cmd}))
   try:rc=p.wait(timeout=240)
   except subprocess.TimeoutExpired:
    os.killpg(p.pid,signal.SIGTERM)
    try:p.wait(timeout=5)
    except subprocess.TimeoutExpired:os.killpg(p.pid,signal.SIGKILL);p.wait(timeout=5)
    rc='timeout'
   (out/'exit.json').write_text(json.dumps({'exit':rc}))
   text=(out/'launcher.log').read_text(errors='replace')
   import re
   plain=re.sub(r'\x1b\[[0-9;]*m','',text)
   (out/'launcher-plain.log').write_text(plain)
   rows=[json.loads(line) for line in (out/'semantic.jsonl').read_text().splitlines()] if (out/'semantic.jsonl').exists() else []
   errors=[line for line in plain.splitlines() if ' ERROR ' in line]
   expected='[Combat Extended] Patch operation Verse.PatchOperationReplace(Defs/ThingDef[@Name="HAR_Solark_BaseNRPawn"]/comps/li[@Class="Solark_Dll.Solark_CompProperties_CommandExplosive"]/explosiveDamageType) failed'
   complete=next((row['data'] for row in reversed(rows) if row['type']=='complete'),None)
   verdict={'semantic':complete,'errors':errors,'steamUninitialized':'SteamAPI.Init() failed' in plain or 'SteamAPI_Init() failed' in plain,'passed':rc==0 and complete=={'failed':0} and all(expected in line for line in errors) and len(errors)==1 and ('SteamAPI.Init() failed' in plain or 'SteamAPI_Init() failed' in plain)}
   (out/'verdict.json').write_text(json.dumps(verdict,indent=2))
 finally:
  if p is not None and p.poll() is None:
   os.killpg(p.pid,signal.SIGKILL);p.wait(timeout=5)
  for link,target in created:
   if link.is_symlink() and os.readlink(link)==str(target):link.unlink()
  after=identities();(out/'inputs-after.json').write_text(json.dumps(after,indent=2));(out/'cleanup.json').write_text(json.dumps({'ownedProcessStopped':p is None or p.poll() is not None,'modsUnchanged':entries==inventory(),'inputsUnchanged':before==after},indent=2))
print(out,flush=True)

if not json.loads((out/'verdict.json').read_text())['passed'] or not all(json.loads((out/'cleanup.json').read_text()).values()):sys.exit(2)
