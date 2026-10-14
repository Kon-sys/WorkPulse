"""Local sequential latency sample; credentials stay in ignored local files."""
from pathlib import Path
import json,time,statistics,urllib.request,http.cookiejar,datetime,platform
root=Path(__file__).resolve().parents[1]
base='http://127.0.0.1:5080/api/v1'
opener=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
def request(path,body=None,csrf=None):
 headers={'Content-Type':'application/json'}
 if csrf:headers['X-CSRF-TOKEN']=csrf
 req=urllib.request.Request(base+path,data=None if body is None else json.dumps(body).encode(),headers=headers)
 with opener.open(req,timeout=30) as response:return json.loads(response.read())
csrf=request('/auth/csrf')['token']
request('/auth/login',{'organizationId':'10000000-0000-0000-0000-000000000001','email':'admin@example.test','password':(root/'tmp/api-demo-password.txt').read_text().strip()},csrf)
request('/auth/csrf')
now=datetime.datetime.now(datetime.timezone.utc)
from urllib.parse import urlencode
paths={'employees':'/employees','violations':'/violations','reports':'/reports?'+urlencode({'start':(now-datetime.timedelta(days=1)).isoformat(),'end':now.isoformat()})}
results={}
for name,path in paths.items():
 request(path);values=[]
 for i in range(30):
  start=time.perf_counter();request(path);values.append((time.perf_counter()-start)*1000)
 values.sort();results[name]={'samples':30,'medianMs':round(statistics.median(values),3),'p95Ms':round(values[28],3),'maxMs':round(max(values),3),'under1000Ms':sum(v<1000 for v in values)}
out=root/'tmp/lr11';out.mkdir(parents=True,exist_ok=True)
(out/'latency-results.json').write_text(json.dumps({'date':now.isoformat(),'scope':'localhost, 1 sequential client, warmed API, existing synthetic database; not a load or production SLA test','os':platform.platform(),'measurements':results},ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(results))
