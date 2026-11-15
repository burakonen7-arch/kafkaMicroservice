#!/usr/bin/env python3
"""Real HTTP -> outbox -> Kafka -> EF Core smoke test; uses unique product IDs."""
import json
import subprocess
import time
import uuid
import sqlite3
from pathlib import Path
from urllib.request import Request, urlopen
from urllib.error import HTTPError

root = Path(__file__).resolve().parents[1]
def request(port, path, data=None, method=None):
    payload = None if data is None else json.dumps(data).encode()
    req = Request(f'http://127.0.0.1:{port}/api/{path}', payload,
                  {'Content-Type': 'application/json'}, method=method)
    with urlopen(req, timeout=10) as response:
        return response.status, json.load(response)

def wait_for(check, timeout=60):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        if check():
            return
        time.sleep(.5)
    raise AssertionError('Expected behavior did not occur before timeout')

run = uuid.uuid4().hex[:10]
products = [f'smoke-{run}-{i}' for i in range(12)]
orders = []
for product in products:
    assert request(5102, f'stocks/{product}', {'quantity': 100}, 'PUT')[0] == 200
    for quantity in [2, 3]:
        status, order = request(5101, 'orders', {'productId': product, 'quantity': quantity})
        assert status == 201
        assert request(5101, f'orders/{order["id"]}')[1] == order
        orders.append(order)
ids = {o['id'] for o in orders}
def events():
    return [e for e in request(5102, 'stocks/events')[1] if e['orderId'] in ids]
wait_for(lambda: len(events()) == len(orders))
for product in products:
    assert request(5102, f'stocks/{product}')[1]['quantity'] == 95
    observed = [e for e in events() if e['productId'] == product]
    assert len({e['partition'] for e in observed}) == 1
assert {e['partition'] for e in events()} == {0, 1, 2}, 'Expected all three partitions'
print('PASS: 24 orders delivered, three partitions used, same product stays in one partition')

# Deliberately publish the same EventId again; stock must not be deducted twice.
with sqlite3.connect(root / 'src/OrderService/OrderService.Api/data/orders.db') as db:
    key, payload = db.execute('SELECT Key, Payload FROM Outbox WHERE Key=? LIMIT 1', (products[0],)).fetchone()
result = subprocess.run(['docker', 'compose', 'exec', '-T', 'kafka',
    '/opt/kafka/bin/kafka-console-producer.sh', '--bootstrap-server', 'localhost:19092',
    '--topic', 'orders.created.v1', '--property', 'parse.key=true', '--property', 'key.separator=|'],
    input=f'{key}|{payload}\n', text=True, cwd=root, check=True)
# A subsequent event with the same key is a processing barrier for the duplicate.
status, barrier = request(5101, 'orders', {'productId': products[0], 'quantity': 1})
ids.add(barrier['id'])
wait_for(lambda: any(e['orderId'] == barrier['id'] for e in events()))
assert request(5102, f'stocks/{products[0]}')[1]['quantity'] == 94
assert len(events()) == 25
print('PASS: replayed event does not deduct stock twice')

_, rejected = request(5101, 'orders', {'productId': products[0], 'quantity': 1000})
ids.add(rejected['id'])
wait_for(lambda: any(e['orderId'] == rejected['id'] for e in events()))
assert next(e for e in events() if e['orderId'] == rejected['id'])['outcome'] == 'InsufficientStock'
assert request(5102, f'stocks/{products[0]}')[1]['quantity'] == 94
for quantity in [0, -1]:
    try:
        request(5101, 'orders', {'productId': products[0], 'quantity': quantity})
        raise AssertionError('Invalid quantity was accepted')
    except HTTPError as error:
        assert error.code == 400
print('PASS: insufficient stock recorded; invalid quantities return HTTP 400')
