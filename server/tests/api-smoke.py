"""Integration checks against a running local API/PostgreSQL; creates isolated synthetic records."""
import json, urllib.request, urllib.error, http.cookiejar, uuid, datetime, time
from pathlib import Path

BASE='http://127.0.0.1:5080/api/v1'
PASSWORD=(Path(__file__).resolve().parents[2]/'tmp/api-demo-password.txt').read_text().strip()
ORG='10000000-0000-0000-0000-000000000001'
OTHER='20000000-0000-0000-0000-000000000001'
passed=0
def check(value,label):
    global passed
    assert value,label
    passed+=1;print('PASS',label)
def now():return datetime.datetime.now(datetime.timezone.utc).isoformat()

class Client:
    def __init__(self):
        self.opener=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
        self.csrf=None
    def call(self,method,path,body=None,status=200,token=None,csrf=True):
        headers={'Content-Type':'application/json'}
        if self.csrf and csrf:headers['X-CSRF-TOKEN']=self.csrf
        if token:headers['Authorization']='Bearer '+token
        request=urllib.request.Request(BASE+path,data=None if body is None else json.dumps(body).encode(),headers=headers,method=method)
        try:
            with self.opener.open(request) as r:code=r.status;raw=r.read()
        except urllib.error.HTTPError as e:code=e.code;raw=e.read()
        assert code==status,(method,path,code,raw.decode()[:300])
        return json.loads(raw) if raw else None
    def login(self,role,org=ORG):
        self.csrf=self.call('GET','/auth/csrf')['token']
        user=self.call('POST','/auth/login',{'organizationId':org,'email':role+'@example.test','password':PASSWORD})
        self.csrf=self.call('GET','/auth/csrf')['token']
        return user

anon=Client();anon.call('GET','/employees',status=401);check(True,'anonymous rejected')
admin=Client();admin.login('admin')
admin.call('POST','/departments',{'code':'CSRF','name':'CSRF'},status=400,csrf=False);check(True,'CSRF enforced')
suffix=uuid.uuid4().hex[:10]
dep=admin.call('POST','/departments',{'code':'T-'+suffix,'name':'Test '+suffix})
employee=admin.call('POST','/employees',{'departmentId':dep['id'],'employeeNumber':'T-'+suffix,'fullName':'API Test '+suffix,'positionName':'Synthetic','hiredOn':'2026-01-01'})
check(employee['organization_id']==ORG,'tenant assigned by authenticated server')
admin.call('POST','/employees',{'departmentId':OTHER,'employeeNumber':'BAD-'+suffix,'fullName':'bad','hiredOn':'2026-01-01'},status=400);check(True,'cross-tenant FK rejected')
hr=Client();hr.login('hr');hr.call('POST','/devices',{'employeeId':employee['id'],'name':'No','platform':'windows'},status=403);check(True,'HR cannot provision devices')
manager=Client();manager.login('manager');manager.call('GET','/employees/'+employee['id']+'/sessions',status=404);check(True,'manager department scope enforced')
other=Client();other.login('admin',OTHER);other.call('GET','/employees/'+employee['id']+'/sessions',status=404);check(True,'other tenant cannot read employee')
device=admin.call('POST','/devices',{'employeeId':employee['id'],'name':'TEST-'+suffix,'platform':'windows'})
agent=Client();agent.call('POST','/agent/events',{'events':[]},status=401);check(True,'agent credentials required')
start=datetime.datetime.now(datetime.timezone.utc)-datetime.timedelta(seconds=60)
events=[{'clientEventId':str(uuid.uuid4()),'occurredAt':start.isoformat(),'kind':'Focus','application':'code.exe'},
        {'clientEventId':str(uuid.uuid4()),'occurredAt':(start+datetime.timedelta(seconds=10)).isoformat(),'kind':'Idle','application':None}]
batch={'events':events}
result=agent.call('POST','/agent/events',batch,token=device['token']);check(len(result['accepted'])==2,'events accepted and session started')
result=agent.call('POST','/agent/events',batch,token=device['token']);check(len(result['duplicates'])==2,'delivery idempotent')
conflict=json.loads(json.dumps(batch));conflict['events'][0]['kind']='Input'
agent.call('POST','/agent/events',conflict,token=device['token'],status=409);check(True,'conflicting duplicate rejected')
session=admin.call('GET','/employees/'+employee['id']+'/sessions')[0]
run=admin.call('POST','/sessions/'+session['id']+'/analysis',{'until':now(),'signalLifetimeSeconds':120})
check(sum((datetime.datetime.fromisoformat(i['end'])-datetime.datetime.fromisoformat(i['start'])).total_seconds() for i in run['intervals'])>50,'analysis timeline persisted')
saved=hr.call('GET','/analysis-runs/'+run['id']);check(saved['id']==run['id'],'HR reads immutable run')
other.call('GET','/analysis-runs/'+run['id'],status=404);check(True,'cross-tenant result hidden')
run2=admin.call('POST','/sessions/'+session['id']+'/analysis',{'until':now(),'signalLifetimeSeconds':120});check(run2['id']!=run['id'],'recalculation creates separate run')
rotated=admin.call('POST','/devices/'+device['id']+'/credentials',{})
agent.call('POST','/agent/events',batch,token=device['token'],status=401);check(True,'rotated token revoked')
agent.call('POST','/agent/events',batch,token=rotated['token']);check(True,'new token works')
admin.call('DELETE','/devices/'+device['id']);agent.call('POST','/agent/events',batch,token=rotated['token'],status=401);check(True,'retired device rejected')
user=Client();access=user.login('employee');rows=user.call('GET','/employees');check(len(rows)==1 and rows[0]['id']==access['employeeId'],'employee sees only self')
user.call('GET','/employees/'+employee['id']+'/sessions',status=404);check(True,'employee cannot inspect coworker')
self_session=user.call('POST','/employees/'+access['employeeId']+'/sessions',{})
user.call('POST','/sessions/'+self_session['id']+'/breaks',{'kind':'lunch'})
user.call('POST','/sessions/'+self_session['id']+'/breaks',{'kind':'lunch'},status=409);check(True,'overlapping lunch rejected')
time.sleep(.05);user.call('POST','/sessions/'+self_session['id']+'/breaks/end',{})
user.call('POST','/sessions/'+self_session['id']+'/breaks',{'kind':'lunch'},status=409);check(True,'second lunch rejected')
user.call('POST','/sessions/'+self_session['id']+'/end',{});check(True,'own session closed')
print(f'{passed} API integration checks passed.')

# Publish a short-threshold policy, then prove late input removes a current violation without rewriting history.
short=admin.call('POST','/policies',{'name':'Short '+suffix,'idleLimitSeconds':1,'nonTargetLimitSeconds':1,'lunchLimitSeconds':1,'timeZone':'Europe/Minsk','rules':[{'resourceId':ORG,'category':'productive'}]})
late_employee=admin.call('POST','/employees',{'departmentId':dep['id'],'employeeNumber':'L-'+suffix,'fullName':'Late '+suffix,'hiredOn':'2026-01-01'})
assignment=admin.call('POST','/policies/'+short['id']+'/assignments',{'scope':'employee','targetId':late_employee['id']})
late_device=admin.call('POST','/devices',{'employeeId':late_employee['id'],'name':'LATE-'+suffix,'platform':'windows'})
t=max(datetime.datetime.now(datetime.timezone.utc),datetime.datetime.fromisoformat(assignment['valid_from']))+datetime.timedelta(milliseconds=100)
agent.call('POST','/agent/events',{'events':[{'clientEventId':str(uuid.uuid4()),'occurredAt':t.isoformat(),'kind':'Focus','application':'code.exe'},{'clientEventId':str(uuid.uuid4()),'occurredAt':(t+datetime.timedelta(milliseconds=50)).isoformat(),'kind':'Idle','application':None}]},token=late_device['token'])
time.sleep(1.6)
late_session=admin.call('GET','/employees/'+late_employee['id']+'/sessions')[0]
until=now();first=admin.call('POST','/sessions/'+late_session['id']+'/analysis',{'until':until,'signalLifetimeSeconds':120})
check(len(first['violations'])==1,'strict idle threshold produces violation')
journal=admin.call('GET','/violations');violation=next(v for v in journal if v['employee_id']==late_employee['id'])
hr.call('PATCH','/violations/'+violation['id'],{'status':'reviewed','comment':'Проверочный комментарий'})
detail=hr.call('GET','/violations/'+violation['id']);check(detail['comments'][0]['body']=='Проверочный комментарий','review comment persisted')
agent.call('POST','/agent/events',{'events':[{'clientEventId':str(uuid.uuid4()),'occurredAt':(t+datetime.timedelta(milliseconds=500)).isoformat(),'kind':'Input','application':'code.exe'}]},token=late_device['token'])
second=admin.call('POST','/sessions/'+late_session['id']+'/analysis',{'until':until,'signalLifetimeSeconds':120})
check(len(second['violations'])==0,'late input recalculates continuous interval')
check(len(admin.call('GET','/analysis-runs/'+first['id'])['violations'])==1,'previous result remains immutable')
check(not any(v['employee_id']==late_employee['id'] for v in admin.call('GET','/violations')),'obsolete violation excluded from current journal')
admin.call('POST','/devices',{'employeeId':late_employee['id'],'name':'SILENT-'+suffix,'platform':'windows'})
time.sleep(.25) # Allow bounded database/host clock skew after registration.
unknown=admin.call('POST','/sessions/'+late_session['id']+'/analysis',{'until':now(),'signalLifetimeSeconds':120})
check(not any(i['category']=='Idle' for i in unknown['intervals']),'silent device prevents unconfirmed idle')
print(f'{passed} total API integration checks passed.')
