#!/usr/bin/env node
// Hook PostToolUse (Edit|Write|MultiEdit).
// - .cs / .csproj  -> `dotnet build` do projeto mais próximo do arquivo editado
// - .ts / .tsx     -> `tsc --noEmit` no frontend (ou e2e)
// Se falhar, sai com código 2: o Claude Code devolve o stderr para o modelo,
// que vê o erro de compilação e corrige na mesma interação.
import { spawnSync } from 'node:child_process';
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';

const input = JSON.parse(readFileSync(0, 'utf8') || '{}');
const filePath = input.tool_input?.file_path ?? input.tool_input?.path;
if (!filePath) process.exit(0);

const projectDir = process.env.CLAUDE_PROJECT_DIR ?? process.cwd();
const abs = path.resolve(filePath);
const ext = path.extname(abs).toLowerCase();

function findUp(startDir, predicate) {
  let dir = startDir;
  while (dir.startsWith(path.resolve(projectDir))) {
    const hit = readdirSync(dir).find(predicate);
    if (hit) return path.join(dir, hit);
    const parent = path.dirname(dir);
    if (parent === dir) break;
    dir = parent;
  }
  return null;
}

// No Windows o spawn usa shell, então caminhos com espaço precisam de aspas.
const q = (s) => (process.platform === 'win32' ? `"${s}"` : s);

// Usa o SDK de DOTNET_ROOT ou ~/.dotnet (instalação por usuário) se existir; senão, o do PATH.
function dotnetExe() {
  const exe = process.platform === 'win32' ? 'dotnet.exe' : 'dotnet';
  const home = process.env.USERPROFILE ?? process.env.HOME ?? '';
  for (const root of [process.env.DOTNET_ROOT, path.join(home, '.dotnet')]) {
    if (root && existsSync(path.join(root, 'sdk')) && existsSync(path.join(root, exe))) {
      return q(path.join(root, exe));
    }
  }
  return 'dotnet';
}

function run(cmd, args, cwd) {
  const r = spawnSync(cmd, args, { cwd, encoding: 'utf8', shell: process.platform === 'win32' });
  return { ok: r.status === 0, out: `${r.stdout ?? ''}${r.stderr ?? ''}` };
}

function fail(title, out) {
  // Mostra só as linhas relevantes para não poluir o contexto do modelo.
  const relevant = out
    .split(/\r?\n/)
    .filter((l) => /error|erro|warning CS|TS\d{4}/i.test(l))
    .slice(0, 30)
    .join('\n');
  process.stderr.write(`${title}\n${relevant || out.slice(-3000)}\n`);
  process.exit(2);
}

if (ext === '.cs' || ext === '.csproj') {
  const csproj = ext === '.csproj' ? abs : findUp(path.dirname(abs), (f) => f.endsWith('.csproj'));
  if (!csproj) process.exit(0);
  const r = run(dotnetExe(), ['build', q(csproj), '--nologo', '-v', 'q', '-clp:NoSummary'], projectDir);
  if (!r.ok) fail(`dotnet build falhou em ${path.basename(csproj)}:`, r.out);
  process.exit(0);
}

if (ext === '.ts' || ext === '.tsx') {
  const tsconfig = findUp(path.dirname(abs), (f) => f === 'tsconfig.json');
  if (!tsconfig) process.exit(0);
  const dir = path.dirname(tsconfig);
  if (!existsSync(path.join(dir, 'node_modules'))) process.exit(0); // deps ainda não instaladas
  const r = run('npx', ['tsc', '-b', '--noEmit'], dir);
  if (!r.ok) fail(`tsc falhou em ${path.relative(projectDir, dir)}:`, r.out);
  process.exit(0);
}

process.exit(0);
