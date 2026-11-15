#!/usr/bin/env python3
"""Optional disruptive test. Stops only this Compose project's Kafka, then restores it."""
import json
import sqlite3
import subprocess
import time
import uuid
from pathlib import Path
from urllib.request import Request, urlopen
root = Path(__file__).resolve().parents[1]
def http(port, path, data=None, method=None):
    req = Request(f'http://127.0.0.1:{port}/api/{path}',
                  None if data is None else json.dumps(data).encode(),
                  {'Content-Type': 'application/json'}, method=method)
    with urlopen(req, timeout=10) as response:
        return json.load(response)
def compose(*args):
    subprocess.run(['docker','compose',*args], cwd=root, check=True)
product = 'offline-' + uuid.uuid4().hex[:10]
http(5102, f'stocks/{product}', {'quantity': 10}, 'PUT')
try:
    compose('stop', 'kafka')
    order = http(5101, 'orders', {'productId': product, 'quantity': 3})
    with sqlite3.connect(root/'src/OrderService/OrderService.Api/data/orders.db') as db:
        assert db.execute('SELECT Published FROM Outbox WHERE Key=?', (product,)).fetchone() == (0,)
    assert http(5102, f'stocks/{product}')['quantity'] == 10
    print('PASS: order persisted with pending outbox while Kafka was stopped')
finally:
    compose('up','-d','--wait','kafka')
end = time.monotonic() + 90
while time.monotonic() < end:
    if http(5102, f'stocks/{product}')['quantity'] == 7:
        break
    time.sleep(1)
else:
    raise AssertionError('Outbox event was not recovered')
print('PASS: Kafka restart recovered pending event and stock became 7')
