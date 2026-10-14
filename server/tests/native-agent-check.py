import importlib.util, pathlib, json, uuid, sys
root=pathlib.Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('smoke',root/'server/tests/api-smoke.py')
# Import only helpers; avoid rerunning smoke scenarios.
source=(root/'server/tests/api-smoke.py').read_text(encoding='utf-8').split('anon=Client()')[0]
namespace={'__file__':str(root/'server/tests/api-smoke.py')};exec(source,namespace)
admin=namespace['Client']();admin.login('admin')
if '--verify' in sys.argv:
 ids=json.loads((root/'tmp/native-agent-ids.json').read_text())
 sessions=admin.call('GET','/employees/'+ids['employee']+'/sessions')
 assert sessions,'Agent did not open session'
 run=admin.call('POST','/sessions/'+sessions[0]['id']+'/analysis',{'until':namespace['now'](),'signalLifetimeSeconds':120})
 assert run['intervals'],'No intervals'
 admin.call('DELETE','/devices/'+ids['device'])
 print('PASS native Windows collection -> HTTP -> PostgreSQL session -> saved analysis; test enrollment retired.')
else:
 suffix=uuid.uuid4().hex[:10]
 e=admin.call('POST','/employees',{'departmentId':namespace['ORG'],'employeeNumber':'NATIVE-'+suffix,'fullName':'Проверка Windows-агента '+suffix,'positionName':'Проверочная запись','hiredOn':'2026-01-01'})
 device=admin.call('POST','/devices',{'employeeId':e['id'],'name':'NATIVE-CHECK-'+suffix,'platform':'windows'})
 (root/'agent/appsettings.Local.json').write_text(json.dumps({'serverUrl':'http://127.0.0.1:5080','token':device['token'],'bufferDirectory':str(root/'tmp'/('native-agent-buffer-'+suffix)),'pollSeconds':1,'idleSeconds':30}),encoding='utf-8')
 (root/'tmp/native-agent-ids.json').write_text(json.dumps({'employee':e['id'],'device':device['id']}),encoding='utf-8')
 print('Temporary native-agent enrollment prepared; token stored only in ignored local configuration.')
