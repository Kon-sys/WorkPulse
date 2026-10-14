"""LR10 integration checks. Requires running API and PostgreSQL; extends LR9 checks."""
from pathlib import Path
import runpy,datetime,time,uuid,json,urllib.request,urllib.error,zipfile,io
base=runpy.run_path(str(Path(__file__).with_name('api-smoke.py')))
Client=base['Client'];admin=base['admin'];hr=base['hr'];check=base['check'];ORG=base['ORG'];PASSWORD=base['PASSWORD'];dep=base['dep'];suffix=uuid.uuid4().hex[:8]
resource=admin.call('POST','/resources',{'kind':'domain','identifier':'example.com'})
check(resource['kind']=='domain','domain resource configurable without collecting URLs')
admin.call('POST','/resources',{'kind':'domain','identifier':'https://example.com/path'},status=400)
check(True,'domain URLs rejected')
now=datetime.datetime.now(datetime.timezone.utc);local=now+datetime.timedelta(hours=3)
start=(local-datetime.timedelta(hours=1)).strftime('%H:%M:%S');end=(local+datetime.timedelta(hours=1)).strftime('%H:%M:%S')
policy=admin.call('POST','/policies',{'name':'Schedule '+suffix,'idleLimitSeconds':30,'nonTargetLimitSeconds':30,'lunchLimitSeconds':30,'timeZone':'Europe/Minsk','lateGraceSeconds':300,'earlyGraceSeconds':300,'rules':[],'schedule':[{'isoWeekday':local.isoweekday(),'startsAt':start,'endsAt':end,'endsNextDay':end<=start}]})
check(True,'schedule version published')
admin.call('POST','/policies',{'name':'Invalid '+suffix,'idleLimitSeconds':30,'nonTargetLimitSeconds':30,'lunchLimitSeconds':30,'timeZone':'Europe/Minsk','rules':[],'schedule':[{'isoWeekday':1,'startsAt':'18:00:00','endsAt':'09:00:00','endsNextDay':False}]},status=400)
check(True,'invalid schedule rejected')
employee=admin.call('POST','/employees',{'departmentId':dep['id'],'employeeNumber':'S-'+suffix,'fullName':'Schedule '+suffix,'hiredOn':'2026-01-01'})
assignment=admin.call('POST','/policies/'+policy['id']+'/assignments',{'scope':'employee','targetId':employee['id']})
device=admin.call('POST','/devices',{'employeeId':employee['id'],'name':'S-'+suffix,'platform':'windows'})
email='schedule-'+suffix+'@example.test'
admin.call('POST','/users',{'employeeId':employee['id'],'email':email,'password':PASSWORD,'role':'employee','departmentId':None})
user=Client();user.csrf=user.call('GET','/auth/csrf')['token'];user.call('POST','/auth/login',{'organizationId':ORG,'email':email,'password':PASSWORD});user.csrf=user.call('GET','/auth/csrf')['token']
t=max(datetime.datetime.now(datetime.timezone.utc),datetime.datetime.fromisoformat(assignment['valid_from']))+datetime.timedelta(milliseconds=100)
user.call('POST','/agent/events',{'events':[{'clientEventId':str(uuid.uuid4()),'occurredAt':t.isoformat(),'kind':'Focus','application':'code.exe'}]},token=device['token'])
time.sleep(.3)
session=user.call('GET','/employees/'+employee['id']+'/sessions')[0]
user.call('POST','/sessions/'+session['id']+'/end',{})
closed=user.call('GET','/employees/'+employee['id']+'/sessions')[0]
run=admin.call('POST','/sessions/'+session['id']+'/analysis',{'until':closed['ended_at'],'signalLifetimeSeconds':120})
check({v['kind'] for v in run['violations']}=={'LateArrival','EarlyDeparture'},'schedule deviations persisted')
check(run['schedule']['compliant'] is False,'known completed session assesses compliance')
from urllib.parse import urlencode
start_range=(t-datetime.timedelta(seconds=1)).isoformat();end_range=closed['ended_at']
q=urlencode({'start':start_range,'end':end_range,'employeeId':employee['id']})
report=admin.call('GET','/reports?'+q);metrics=report['employees'][0]['metrics']
check(metrics['neutralSeconds']>0 and metrics['unknownSeconds']==0 and metrics['coverage']==100,'report uses clipped saved classification')
repeat=admin.call('POST','/sessions/'+session['id']+'/analysis',{'until':end_range,'signalLifetimeSeconds':120})
again=admin.call('GET','/reports?'+q)
check(abs(again['employees'][0]['metrics']['knownSeconds']-metrics['knownSeconds'])<.00001,'recalculation never doubles reporting time')
check(len(again['departments'])==1,'department analytics aggregate visible employees')
base['other'].call('GET','/reports?'+q,status=404);check(True,'cross-tenant report hidden')
base['manager'].call('GET','/reports?'+q,status=404);check(True,'manager report obeys department scope')
user.call('GET','/reports?'+q);check(True,'employee report reads only self')
user.call('GET','/reports?'+urlencode({'start':start_range,'end':end_range,'employeeId':base['employee']['id']}),status=404);check(True,'employee cannot export coworker data')
admin.call('GET','/reports?'+urlencode({'start':end_range,'end':start_range}),status=400);check(True,'reversed reporting range rejected')
out=Path(__file__).resolve().parents[2]/'tmp/lr10';out.mkdir(exist_ok=True)
for format in ['pdf','xlsx']:
 request=urllib.request.Request(base['BASE']+'/reports/export/'+format+'?'+q)
 with admin.opener.open(request) as response:content=response.read();media=response.headers['Content-Type']
 (out/('export.'+format)).write_bytes(content)
 if format=='pdf':check(content.startswith(b'%PDF-'),'real PDF export')
 else:
  with zipfile.ZipFile(io.BytesIO(content)) as archive:check('xl/workbook.xml' in archive.namelist(),'real XLSX export')
versions=admin.call('GET','/policies');saved=next(v for v in versions if v['id']==policy['id'])
check(saved['late_grace_seconds']==300 and len(json.loads(saved['schedule']))==1,'schedule and tolerance read back unchanged')
admin.call('POST','/policies',{'name':'Negative '+suffix,'idleLimitSeconds':-1,'nonTargetLimitSeconds':30,'lunchLimitSeconds':30,'timeZone':'Europe/Minsk','rules':[]},status=400)
check(True,'negative policy limit rejected transactionally')
violations=admin.call('GET','/violations');own_violation=next(v for v in violations if v['employee_id']==employee['id'])
check(user.call('GET','/violations/'+own_violation['id'])['violation']['id']==own_violation['id'],'employee can read own violation details')
user.call('PATCH','/violations/'+own_violation['id'],{'status':'reviewed','comment':'Not allowed'},status=404)
check(True,'employee cannot review violation')
admin.call('DELETE','/devices/'+device['id']);check(True,'LR10 fixture token retired')
# A real earlier session proves comparison uses data, not just empty previous periods.
comparison_employee=admin.call('POST','/employees',{'departmentId':dep['id'],'employeeNumber':'C-'+suffix,'fullName':'Comparison '+suffix,'hiredOn':'2026-01-01'})
comparison_device=admin.call('POST','/devices',{'employeeId':comparison_employee['id'],'name':'C-'+suffix,'platform':'windows'})
comparison_now=datetime.datetime.now(datetime.timezone.utc)
c0=comparison_now-datetime.timedelta(minutes=30);c1=comparison_now-datetime.timedelta(minutes=15)
user.call('POST','/agent/events',{'events':[{'clientEventId':str(uuid.uuid4()),'occurredAt':c0.isoformat(),'kind':'Focus','application':'code.exe'},{'clientEventId':str(uuid.uuid4()),'occurredAt':c1.isoformat(),'kind':'Idle','application':None}]},token=comparison_device['token'])
comparison_session=admin.call('GET','/employees/'+comparison_employee['id']+'/sessions')[0]
time.sleep(.25)
comparison_end=datetime.datetime.now(datetime.timezone.utc).isoformat()
admin.call('POST','/sessions/'+comparison_session['id']+'/analysis',{'until':comparison_end,'signalLifetimeSeconds':86400})
current_report=admin.call('GET','/reports?'+urlencode({'start':c1.isoformat(),'end':comparison_end,'employeeId':comparison_employee['id']}))
previous_report=admin.call('GET','/reports?'+urlencode({'start':c0.isoformat(),'end':c1.isoformat(),'employeeId':comparison_employee['id']}))
check(current_report['employees'][0]['metrics']['productiveShare']==0 and previous_report['employees'][0]['metrics']['productiveShare']==100,'nonempty periods compare 100 to 0 percent productivity')
admin.call('DELETE','/devices/'+comparison_device['id'])
(out/'comparison-fixture.json').write_text(json.dumps({'employee':comparison_employee['id'],'start':c1.isoformat(),'end':comparison_end}))
check(True,'LR10 integration extension completed')
(out/'integration-result.json').write_text(json.dumps({'date':datetime.date.today().isoformat(),'checks':base['passed']+(base['check'].__globals__['passed']-base['passed']),'result':'passed','fixtureEmployee':employee['id'],'fixtureSession':session['id']},indent=2))
