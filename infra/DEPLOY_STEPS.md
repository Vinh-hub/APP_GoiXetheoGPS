# Deployment Steps (Nhi + Tri)

## 1) Cai dat MySQL config

Copy file tu `mysql/` vao `mysqld.cnf` tren tung may:

- North master -> `north-master.cnf`
- North slave -> `north-slave.cnf`
- South master -> `south-master.cnf`
- South slave -> `south-slave.cnf`

Restart MySQL:

```bash
sudo systemctl restart mysql
```

## 2) Tao user replication + ket noi slave

Chay `mysql/replication_setup.sql` tren master.
Sau do tren slave chay:

```sql
STOP SLAVE;
CHANGE MASTER TO
  MASTER_HOST='MASTER_IP',
  MASTER_USER='repl',
  MASTER_PASSWORD='repl@123',
  MASTER_AUTO_POSITION=1;
START SLAVE;
SHOW SLAVE STATUS\G
```

## 3) Cai Keepalived + script health check

Copy:
- `scripts/check_mysql.sh` -> `/etc/keepalived/check_mysql.sh`
- file `keepalived/*.conf` -> `/etc/keepalived/keepalived.conf` theo dung vai tro may.

Cap quyen script:

```bash
sudo chmod +x /etc/keepalived/check_mysql.sh
```

Enable + restart:

```bash
sudo systemctl enable keepalived
sudo systemctl restart keepalived
```

## 4) Monitor + logging

Copy:
- `scripts/monitor_replication.sh` -> `/usr/local/bin/monitor_replication.sh`
- `scripts/failover_event_logger.sh` -> `/usr/local/bin/failover_event_logger.sh`

Cap quyen:

```bash
sudo chmod +x /usr/local/bin/monitor_replication.sh
sudo chmod +x /usr/local/bin/failover_event_logger.sh
```

Cron moi 1 phut:

```bash
* * * * * MYSQL_PWD=yourpass /usr/local/bin/monitor_replication.sh NORTH
* * * * * MYSQL_PWD=yourpass /usr/local/bin/monitor_replication.sh SOUTH
```

## 5) Chay test checklist

Thuc hien theo `tests/TEST_CHECKLIST.md` va luu screenshot/log de dua vao bao cao.
