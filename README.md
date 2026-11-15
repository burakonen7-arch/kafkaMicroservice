# Kafka ile .NET 10 mikroservis öğrenme uygulaması

OrderService siparişi kaydeder, `orders.created.v1` topic'ine `OrderCreated` olayı gönderir. StockService olayı tüketir ve stok yeterliyse miktarı azaltır. Stok yetersizse `InsufficientStock` sonucu kaydedilir. Siparişin HTTP 201 dönmesi siparişin kaydedildiği anlamındadır; stok işlemi asenkron gerçekleşir.

## Mimari

Her servisin bağımsız EF Core / SQLite veritabanı vardır.

```text
Api (Controllers, DI)
 ├── Application (servis arayüzleri, servisler, DbContext sözleşmesi)
 │    └── Domain (varlıklar)
 └── Infrastructure (EF Core DbContext, Kafka producer/consumer)
      └── Application
Contracts (sürümlenen olay sözleşmesi)
```

Repository katmanı yoktur. Application servisleri `IOrderDbContext` / `IStockDbContext` üzerinden EF Core kullanır; bu arayüzler DbSet ve SaveChanges sunar. Application, EF Core'a bağımlıdır; SQLite sağlayıcısı ve Kafka istemcisi Infrastructure katmanındadır. Program.cs yalnızca servis kayıtları, başlangıç ve controller yönlendirmesini içerir. HTTP uçları Controllers klasöründedir.

Sipariş ile outbox kaydı tek `SaveChanges` işleminde atomik yazılır. Tek outbox worker mesajları kayıt sırasıyla gönderir, broker onayından sonra yayımlandı olarak işaretler. DB işaretlemesinden önce kesinti olursa olay tekrar gönderilebilir. StockService, EventId kaydını ve stok değişimini aynı işlemde yazar; Kafka offset'ini bundan sonra commit eder. Böylece teslimat en az bir kez gerçekleşir ve tekrar gelen olay stoktan ikinci kez düşmez.

## Kurulum ve başlatma

Gerekenler: .NET SDK 10.0.100 (veya uyumlu 10.0.1xx yaması), Docker + Compose v2, Bash, curl; test için Python 3. Bulut ortamında SDK `/workspace/.dotnet` altına kurulur. Kurulum betiği SDK bulunmazsa bu konuma indirir. `DOTNET_ROOT`, `DOTNET_CLI_HOME` ve `NUGET_PACKAGES` ile yollar özelleştirilebilir.

Depo kökünde:

```bash
bash scripts/setup.sh
bash scripts/start.sh
```

İkinci terminalde:

```bash
python3 scripts/smoke.py
# İsteğe bağlı: bu projedeki Kafka'yı kısa süre durdurup yeniden açar.
python3 scripts/resilience.py
```

`start.sh` ön planda kalır; Ctrl+C iki API'yi durdurur. Kafka kalıcı volume ile çalışmaya devam eder. Kafka'yı durdurmak için `docker compose stop`; veriyi koruyarak container'ları kaldırmak için `docker compose down` kullanılır. `down -v` Kafka verisini siler; normal yeniden başlatmada kullanmayın. API SQLite dosyaları her API'nin `data/` klasöründedir. Betik yeniden başlatmalarında korunur. Loglar `.logs/order.log` ve `.logs/stock.log` dosyalarına yazılır. Aynı API portlarında ikinci start.sh çalıştırmayın.

Bağımlılıklar `packages.lock.json` dosyalarıyla kilitlidir. Bilinçli paket değişikliğinde `dotnet restore KafkaMicroservice.slnx --use-lock-file` çalıştırıp kilit dosyalarını güncelleyin. `setup.sh`, `--locked-mode` ile kurar ve derler.

## API örneği

```bash
# Başlangıç stoğu: 100
curl -i -X PUT http://127.0.0.1:5102/api/stocks/product-1 \
  -H 'Content-Type: application/json' -d '{"quantity":100}'

# Sipariş oluştur
curl -i -X POST http://127.0.0.1:5101/api/orders \
  -H 'Content-Type: application/json' -d '{"productId":"product-1","quantity":3}'

# İşlendikten sonra miktar 97 olur
curl http://127.0.0.1:5102/api/stocks/product-1

# OrderId, EventId (id), outcome, partition ve offset
curl http://127.0.0.1:5102/api/stocks/events

# Sipariş kimliğini POST yanıtından alın
# GET /api/orders/{id}
```

Stok PUT çağrısı geliştirme/test amaçlı mutlak miktar belirler. Siparişlerle eşzamanlı stok yönetimi için tasarlanmış bir rezervasyon API'si değildir.

## Kafka ve partition deneyleri

Kafka 4.1.1, KRaft ile çalışır; ZooKeeper gerekmez. Topic açıkça oluşturulur: `orders.created.v1`, **3 partition**, replication factor **1**. Host uygulamaları `localhost:9092`, Docker ağı içindeki araçlar `kafka:19092` kullanır. Otomatik topic oluşturma kapalıdır.

Mesajın **key** alanı ProductId'dir. Aynı key, partition sayısı sabitken aynı partition'a gider. Sıralama yalnızca partition içinde garanti edilir; farklı ürünler arasında global sıra yoktur. Partition sayısını artırmak key dağılımını değiştirebilir. Farklı ürünler aynı partition'a da düşebilir.

```bash
# Üç partition ve liderlerini gör
docker compose exec kafka /opt/kafka/bin/kafka-topics.sh \
  --bootstrap-server localhost:19092 --describe --topic orders.created.v1

# Key, partition, offset ve mesaj gövdesini izle; Ctrl+C ile çık
docker compose exec kafka /opt/kafka/bin/kafka-console-consumer.sh \
  --bootstrap-server localhost:19092 --topic orders.created.v1 --from-beginning \
  --property print.key=true --property print.partition=true --property print.offset=true

# Consumer group, partition dağılımı ve lag
docker compose exec kafka /opt/kafka/bin/kafka-consumer-groups.sh \
  --bootstrap-server localhost:19092 --describe --group stock-service
```

İkinci StockService instance'ını ayrı terminalde, aynı API çalışma dizininde açın (bulutta PATH'e `/workspace/.dotnet` ekleyin):

```bash
cd src/StockService/StockService.Api
Kafka__GroupId=stock-service dotnet bin/Debug/net10.0/StockService.Api.dll \
  --urls http://127.0.0.1:5103
```

Aynı çalışma dizini iki consumer'ın aynı SQLite dosyasını kullanmasını sağlar. Başlatılan ana instance `5102`, ikinci instance `5103` portundadır. Aynı gruptaki consumer'lar partition'ları paylaşır; rebalance loglarda görünür. Üç partition için en fazla üç consumer eşzamanlı çalışır; dördüncü bekler. Yeni instance ekleyip çıkararak `kafka-consumer-groups.sh` çıktısını karşılaştırın. Farklı group adı her grubun tüm mesajları bağımsız okumasını sağlar; aynı DB kullanılırsa EventId kontrolü eski stok işlemlerinin tekrar uygulanmasını engeller.

`Kafka__BootstrapServers`, `Kafka__Topic`, `Kafka__GroupId` ve `ConnectionStrings__Database` ortam değişkenleriyle ayarlar değiştirilebilir. Yeni topic adı seçerseniz topic'i ayrıca oluşturun.

## Kontroller ve kapsam

`smoke.py` gerçek HTTP/Kafka/SQLite üzerinde sipariş okuma, stok düşümü, üç partition, aynı key'in aynı partition'da kalması, olayın tekrar teslimi, yetersiz stok ve HTTP 400 davranışını kontrol eder. Her çalışmada benzersiz test ürünleri oluşturur; geliştirme verisini silmez. `resilience.py` Kafka kapalıyken outbox kaydının beklediğini ve broker geri gelince işlendiğini kontrol eder. Testler servisler çalışırken yürütülür.

Bu başlangıç öğrenme ortamıdır: tek broker ve PLAINTEXT bağlantı kullanır. Dışarıya açık üretim dağıtımı için kimlik doğrulama/TLS, çoklu broker ve uygun replication gerekir. Başlangıçta `EnsureCreated` kullanılır; şema değişikliklerinde EF migration yönetimi eklenmelidir. Tek OrderService/outbox publisher instance'ı varsayılır; çoklu publisher için claim/locking gerekir. Hatalı mesaj aynı offset'te yeniden denenir ve o consumer'ın ilerlemesini durdurur; DLQ ve sınırlı retry sonraki geliştirmedir. StockService sonucu OrderService'e geri bildiren saga/rezervasyon akışı henüz yoktur.
