import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, '..');
const ref = 'mykyntus_v3';
const repo = 'PROJETMYKYNTUS/projets_mykyntus-';

const files = [
  'PrimeBackend/Services/PrimeValidationWorkflowService.cs',
  'PrimeBackend/Services/PrimeValidationWorkflowRuntime.cs',
  'PrimeBackend/Services/PrimeValidationListService.cs',
  'PrimeBackend/Services/PrimeFicheValidationSubmissionService.cs',
  'PrimeBackend/Services/PrimeFicheValidationHistoryService.cs',
  'PrimeBackend/Services/PrimeRbacReadService.cs',
  'PrimeBackend/Services/GlobalPoolWorkflowService.cs',
  'PrimeBackend/Services/PrimeGlobalSynthesisService.cs',
  'PrimeBackend/Services/PrimeGlobalSynthesisReadinessService.cs',
  'PrimeBackend/Services/PrimeGlobalSynthesisPaymentService.cs',
  'PrimeBackend/Services/WorkflowStepConfigRechain.cs',
  'PrimeBackend/Controllers/PrimeValidationController.cs',
  'PrimeBackend/Controllers/PrimeGlobalPoolStakeholderController.cs',
  'PrimeBackend/Controllers/PrimeGlobalPoolScopeController.cs',
  'PrimeBackend/Controllers/GlobalPoolWorkflowAdminController.cs',
  'PrimeBackend/Dto/PrimeWorkflowDtos.cs',
  'PrimeBackend/Dto/PrimeGlobalPoolDtos.cs',
  'PrimeBackend/Data/PrimeDbSeeder.cs',
  'prime-angular/src/app/prime/pages/prime-validation-page.component.ts',
  'prime-angular/src/app/prime/pages/prime-validation-history-page.component.ts',
  'prime-angular/src/app/prime/pages/prime-global-pool-page.component.ts',
  'prime-angular/src/app/prime/pages/admin/admin-workflow.component.ts',
  'prime-angular/src/app/prime/components/prime-fiche-validation-timeline.component.ts',
  'prime-angular/src/app/prime/components/admin/workflow-config-admin.component.ts',
  'prime-angular/src/app/prime/components/admin/workflow-admin.component.ts',
  'prime-angular/src/app/prime/services/prime-fiche-result.service.ts',
  'prime-angular/src/app/prime/services/prime-global-pool-api.service.ts',
  'prime-angular/src/app/prime/lib/workflow-step-rechain.ts',
  'prime-angular/src/app/prime/lib/workflow-role-match.ts',
];

async function fetchFile(filePath) {
  const url = `https://api.github.com/repos/${repo}/contents/${encodeURIComponent(filePath).replace(/%2F/g, '/')}?ref=${ref}`;
  const res = await fetch(url, {
    headers: { Accept: 'application/vnd.github+json', 'User-Agent': 'restore-workflow-script' },
  });
  if (!res.ok) throw new Error(`${filePath}: HTTP ${res.status}`);
  const data = await res.json();
  if (data.encoding !== 'base64' || !data.content) throw new Error(`${filePath}: no base64 content`);
  return Buffer.from(data.content.replace(/\n/g, ''), 'base64').toString('utf8');
}

const log = [];
for (const f of files) {
  try {
    const content = await fetchFile(f);
    const dest = path.join(root, f);
    fs.mkdirSync(path.dirname(dest), { recursive: true });
    fs.writeFileSync(dest, content, 'utf8');
    log.push(`OK ${f}`);
  } catch (e) {
    log.push(`FAIL ${f}: ${e.message}`);
  }
}
const logPath = path.join(root, '_workflow_restore.log');
fs.writeFileSync(logPath, log.join('\n'), 'utf8');
console.log(log.join('\n'));
console.log(`\nLog: ${logPath}`);
