#!/usr/bin/env node
// Hook PreToolUse (Bash).
// O histórico de commits é avaliado no teste: não pode haver squash nem reescrita.
// Bloqueia (exit 2) comandos git que reescrevem ou descartam histórico.
import { readFileSync } from 'node:fs';

const input = JSON.parse(readFileSync(0, 'utf8') || '{}');
const cmd = String(input.tool_input?.command ?? '');

const rules = [
  [/\bgit\s+push\b[^\n]*(--force\b|--force-with-lease\b|\s-f\b)/, 'push forçado'],
  [/\bgit\s+rebase\b/, 'rebase'],
  [/\bgit\s+commit\b[^\n]*--amend\b/, 'commit --amend'],
  [/\bgit\s+reset\b[^\n]*--hard\b/, 'reset --hard'],
  [/\bgit\s+merge\b[^\n]*--squash\b/, 'merge --squash'],
  [/\bgit\s+filter-(branch|repo)\b/, 'filter-branch/filter-repo'],
];

for (const [re, name] of rules) {
  if (re.test(cmd)) {
    process.stderr.write(
      `Bloqueado pelo hook guard-git: "${name}" reescreve/descarta histórico. ` +
        'Este repositório preserva o histórico de commits (requisito do teste). ' +
        'Crie um novo commit corrigindo o problema em vez disso.\n',
    );
    process.exit(2);
  }
}
process.exit(0);
