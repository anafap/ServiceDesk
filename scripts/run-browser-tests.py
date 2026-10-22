#!/usr/bin/env python3
"""Run real browser tests against a uniquely named disposable SQL database."""
import os
from pathlib import Path
import secrets
import subprocess
import tempfile
import time
import urllib.error
import urllib.request
import uuid

ROOT = Path(__file__).resolve().parents[1]
connection = os.environ.get('SERVICEDESK_TEST_SQL_CONNECTION')
if not connection:
    raise SystemExit('Set SERVICEDESK_TEST_SQL_CONNECTION privately to a local SQL Server login allowed to create/drop test databases.')
env = os.environ.copy()
env.update({
    'ConnectionStrings__DefaultConnection': connection,
    'ASPNETCORE_ENVIRONMENT': 'Development', 'ASPNETCORE_URLS': 'http://127.0.0.1:5201',
    'SERVICEDESK_BROWSER_RUN_ID': uuid.uuid4().hex,
    'Jwt__Key': secrets.token_hex(32), 'Jwt__Issuer': 'ServiceDesk.BrowserTests', 'Jwt__Audience': 'ServiceDesk.BrowserTests',
    'DemoUser__Email': '', 'DemoUser__Password': '',
    'LoginRateLimit__PermitLimit': '1000',
    'VITE_API_TARGET': 'http://127.0.0.1:5201',
    'E2E_BASE_URL': 'http://127.0.0.1:5174',
    'E2E_NEW_PASSWORD': secrets.token_urlsafe(16),
})
for account in ['Admin', 'Manager1', 'Manager2']:
    email = account.lower() + '@example.test'
    password = secrets.token_urlsafe(24)
    env[f'DemoData__{account}__Email'] = env[f'E2E_{account.upper()}_EMAIL'] = email
    env[f'DemoData__{account}__Password'] = env[f'E2E_{account.upper()}_PASSWORD'] = password
logs = Path(tempfile.mkdtemp(prefix='servicedesk-browser-'))
processes = []

def run(command):
    return subprocess.run(command, cwd=ROOT, env=env, check=True)

def start(command, name):
    output = (logs / (name + '.log')).open('w')
    process = subprocess.Popen(command, cwd=ROOT, env=env, stdout=output, stderr=subprocess.STDOUT)
    processes.append((process, output))
    return process

def wait_for(url, expected, process):
    deadline = time.monotonic() + 90
    while time.monotonic() < deadline:
        if process.poll() is not None:
            raise RuntimeError('Test service exited. Inspect private logs: ' + str(logs))
        try:
            with urllib.request.urlopen(url, timeout=2) as response:
                if response.status == expected: return
        except urllib.error.HTTPError as error:
            if error.code == expected: return
        except (urllib.error.URLError, TimeoutError): pass
        time.sleep(.2)
    raise RuntimeError('Test service was not ready. Inspect private logs: ' + str(logs))

try:
    print('Building browser test host; original database is never used.', flush=True)
    run(['dotnet', 'build', '--no-restore', '-m:1', '-p:UseSharedCompilation=false', '-nodeReuse:false'])
    api = start(['dotnet', 'run', '--no-build', '--no-restore', '--project', 'ServiceDesk.Api', '--no-launch-profile', '--', '--urls', 'http://127.0.0.1:5201', '--browser-test-host'], 'api')
    wait_for('http://127.0.0.1:5201/api/tickets', 401, api)
    frontend = start(['npm', '--prefix', 'frontend', 'run', 'dev', '--', '--host', '127.0.0.1', '--port', '5174', '--strictPort'], 'frontend')
    wait_for('http://127.0.0.1:5174', 200, frontend)
    print('Running Chromium walkthrough on disposable data.', flush=True)
    run(['npm', '--prefix', 'frontend', 'run', 'test:browser'])
finally:
    for process, output in reversed(processes):
        process.terminate()
        try: process.wait(timeout=15)
        except subprocess.TimeoutExpired: process.kill(); process.wait()
        output.close()
    print('Removing only this run\'s disposable browser database.', flush=True)
    cleanup = subprocess.run(['dotnet', 'run', '--no-build', '--no-restore', '--project', 'ServiceDesk.Api', '--no-launch-profile', '--', '--cleanup-browser-tests'], cwd=ROOT, env=env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    if cleanup.returncode:
        raise RuntimeError('Browser database cleanup failed. Run ID: ' + env['SERVICEDESK_BROWSER_RUN_ID'])
