const {chromium}=require('playwright');
const fs=require('fs');const path=require('path');
async function main(){
 fs.mkdirSync(path.resolve(__dirname,'../../tmp/lr10'),{recursive:true});
 const browser=await chromium.launch({channel:'chrome',headless:true});
 const context=await browser.newContext({viewport:{width:1440,height:1000},acceptDownloads:true});
 const page=await context.newPage();let errors=[];page.on('pageerror',e=>errors.push(e.message));
 const password=fs.readFileSync(path.resolve(__dirname,'../../tmp/api-demo-password.txt'),'utf8').trim();
 await page.goto('http://127.0.0.1:4173');await page.getByLabel('Email',{exact:true}).fill('admin@example.test');await page.getByLabel('Пароль',{exact:true}).fill(password);await page.getByRole('button',{name:'Войти',exact:true}).click();
 await page.getByRole('heading',{name:'Команда в рабочем ритме'}).waitFor();
 await page.waitForTimeout(450);await page.screenshot({path:path.resolve(__dirname,'../../tmp/lr10/lr9-overview-desktop.png'),fullPage:true});
 for(const tab of ['Сотрудники','Нарушения','Политики','Отчёты','Настройки']){await page.getByRole('navigation').getByRole('button',{name:tab,exact:true}).click();await page.getByRole('heading',{name:tab,exact:true}).waitFor();}
 // Policy/resource forms and violation filters are exercised against the live server.
 await page.getByRole('navigation').getByRole('button',{name:'Политики',exact:true}).click();
 await page.getByRole('button',{name:'Добавить ресурс',exact:true}).click();
 const resourceName='ui-'+Date.now()+'.exe';
 await page.getByLabel('Название',{exact:true}).fill(resourceName);
 await page.getByRole('button',{name:'Сохранить',exact:true}).click();
 await page.getByRole('dialog').waitFor({state:'hidden'});
 await page.getByRole('button',{name:'Добавить',exact:true}).click();
 await page.getByLabel('Название',{exact:true}).fill('UI schedule '+Date.now());
 await page.locator('.live-field').filter({hasText:resourceName}).locator('select').selectOption('productive',{timeout:5000}).catch(async e=>{await page.screenshot({path:path.resolve(__dirname,'../../tmp/lr10/policy-debug.png'),fullPage:true});throw new Error('Policy fields: '+(await page.locator('.live-form').innerText()).slice(-1600));});
 if(!await page.getByLabel('Часовой пояс IANA').count())throw new Error('Timezone field absent');
 if(!await page.getByLabel('Пн начало').count())throw new Error('Weekly schedule absent');
 await page.getByRole('button',{name:'Сохранить',exact:true}).click();
 await page.getByRole('dialog').waitFor({state:'hidden'});
 await page.getByRole('navigation').getByRole('button',{name:'Нарушения',exact:true}).click();
 await page.getByLabel(/^Подразделение/).selectOption({index:1});
 await page.getByLabel(/^Подразделение/).selectOption('');
 await page.getByLabel(/^Тип/).selectOption('late_arrival');
 await page.getByLabel('С',{exact:true}).fill('2026-01-01');
 await page.getByLabel('По',{exact:true}).fill('2026-12-31');
 if(!await page.getByRole('button',{name:'Рассмотреть',exact:true}).count())throw new Error('No violation fixture');
 {
  await page.getByRole('button',{name:'Рассмотреть',exact:true}).first().click();
  await page.getByLabel('Комментарий',{exact:true}).fill('Проверено через UI ЛР10');
  await page.getByRole('button',{name:'Сохранить',exact:true}).click();
  await page.getByRole('dialog').waitFor({state:'hidden'});
  await page.getByRole('button',{name:'Рассмотреть',exact:true}).first().click();
  await page.getByText('Проверено через UI ЛР10').first().waitFor();
  await page.getByRole('button',{name:'Закрыть форму',exact:true}).click();
 }
 await page.getByLabel(/^Тип/).selectOption('');
 await page.getByLabel('Фильтр нарушений',{exact:true}).fill('no-matching-employee-'+Date.now());
 await page.getByText('Нарушений по выбранным фильтрам нет.',{exact:true}).waitFor();
 await page.getByLabel('Фильтр нарушений',{exact:true}).fill('');
 await page.getByRole('navigation').getByRole('button',{name:'Сотрудники',exact:true}).click();
 await page.getByRole('button').filter({hasText:'API Test'}).first().click();
 await page.getByRole('heading',{name:'История анализов'}).waitFor();
 await page.locator('.live-run-button').first().click();await page.locator('.live-analysis').waitFor();
 await page.waitForTimeout(450);await page.screenshot({path:path.resolve(__dirname,'../../tmp/lr10/lr9-analysis-desktop.png'),fullPage:true});
 const downloadPromise=page.waitForEvent('download');await page.getByRole('button',{name:'CSV',exact:true}).click();const download=await downloadPromise;await download.saveAs(path.resolve(__dirname,'../../tmp/lr10/lr9-ui-export.csv'));
 if(!fs.readFileSync(path.resolve(__dirname,'../../tmp/lr10/lr9-ui-export.csv'),'utf8').includes('Категория'))throw new Error('CSV missing data');
 await page.getByRole('button',{name:'Закрыть карточку'}).click();await page.setViewportSize({width:390,height:844});await page.getByRole('navigation').getByRole('button',{name:'Обзор',exact:true}).click();
 await page.waitForTimeout(450);await page.screenshot({path:path.resolve(__dirname,'../../tmp/lr10/lr9-overview-mobile.png'),fullPage:true});
 if(await page.evaluate(()=>document.documentElement.scrollWidth>window.innerWidth))throw new Error('Mobile horizontal overflow');
 // Test real report data and both downloads before switching roles.
 await page.setViewportSize({width:1440,height:1000});
 await page.getByRole('navigation').getByRole('button',{name:'Отчёты',exact:true}).click();
 await page.getByRole('button',{name:'Сформировать',exact:true}).click();
 await page.getByRole('button',{name:'Скачать PDF',exact:true}).waitFor();
 for(const format of ['PDF','XLSX']){
   const waiting=page.waitForEvent('download');
   await page.getByRole('button',{name:'Скачать '+format,exact:true}).click();
   const file=await waiting;await file.saveAs(path.resolve(__dirname,'../../tmp/lr10/ui-export.'+format.toLowerCase()));
 }
 if(!await page.getByRole('columnheader',{name:'Изменение продуктивности'}).count())throw new Error('Comparison absent');
 const fixturePath=path.resolve(__dirname,'../../tmp/lr10/comparison-fixture.json');
 if(!fs.existsSync(fixturePath))throw new Error('Run LR10 integration fixture first');
 const fixture=JSON.parse(fs.readFileSync(fixturePath,'utf8'));
 const toLocal=value=>{const d=new Date(value);return new Date(d.getTime()-d.getTimezoneOffset()*60000).toISOString().slice(0,23);};
 await page.getByLabel('С',{exact:true}).fill(toLocal(fixture.start));
 await page.getByLabel('По',{exact:true}).fill(toLocal(fixture.end));
 await page.getByLabel(/^Сотрудник/).selectOption(fixture.employee);
 await page.getByRole('button',{name:'Сформировать',exact:true}).click();
 await page.getByText('-100.0 п.п.',{exact:true}).waitFor();
 await page.screenshot({path:path.resolve(__dirname,'../../tmp/lr10/reports-desktop.png'),fullPage:true});
 await page.setViewportSize({width:390,height:844});
 if(await page.evaluate(()=>document.documentElement.scrollWidth>window.innerWidth))throw new Error('Reports mobile overflow');
 await page.screenshot({path:path.resolve(__dirname,'../../tmp/lr10/reports-mobile.png'),fullPage:true});
 await page.getByRole('button',{name:'Выйти',exact:true}).click();await page.getByLabel('Email',{exact:true}).fill('hr@example.test');await page.getByLabel('Пароль',{exact:true}).fill(password);await page.getByRole('button',{name:'Войти',exact:true}).click();await page.getByRole('heading',{name:'Команда в рабочем ритме'}).waitFor();
 await page.getByRole('navigation').getByRole('button',{name:'Настройки',exact:true}).click();
 if(await page.getByRole('button',{name:'Зарегистрировать устройство'}).count())throw new Error('HR sees admin action');
 await page.getByRole('button',{name:'Выйти',exact:true}).click();
 await page.getByLabel(/^Организация/).selectOption('20000000-0000-0000-0000-000000000001');
 await page.getByLabel('Email',{exact:true}).fill('admin@example.test');
 await page.getByLabel('Пароль',{exact:true}).fill(password);
 await page.getByRole('button',{name:'Войти',exact:true}).click();
 await page.getByRole('heading',{name:'Команда в рабочем ритме'}).waitFor();
 await page.getByRole('navigation').getByRole('button',{name:'Нарушения',exact:true}).click();
 await page.getByText('Нарушений по выбранным фильтрам нет.',{exact:true}).waitFor();
 await page.getByRole('button',{name:'Выйти',exact:true}).click();
 if(errors.length)throw new Error(errors.join('\n'));
 fs.writeFileSync(path.resolve(__dirname,'../../tmp/lr10/ui-result.json'),JSON.stringify({result:'passed',date:new Date().toISOString(),flows:['policy/resource forms','weekly schedule','violation filters/comments','reports','PDF/XLSX','100-to-0 comparison','mobile','role reset']},null,2));
 console.log('PASS UI policy/resource forms, weekly schedule, violation filters/comments, reports, PDF/XLSX, comparison, mobile reports, login, six sections, saved analysis, CSV, mobile overflow, HR controls, no JS errors.');
 await browser.close();
}
main().catch(e=>{console.error(e.message);process.exit(1)});
