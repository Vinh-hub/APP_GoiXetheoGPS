# Nhi + Tri Deliverables (Replication + Failover)

*(Thư mục nằm trong project MAUI: `APP_GoiXetheoGPS/infra` — chỉ dùng khi triển khai Linux/VM; không đóng gói vào file cài app.)*

Muc tieu: hoan thien phan Replication + Failover cho 2 vung North/South theo dung yeu cau:

- Moi vung co 1 master + 1 slave.
- Slave luon `read_only=1` (khong promote thanh master).
- Dung VIP de backend luon ket noi 1 dau moi vung.
- Master down thi VIP chuyen sang slave, he thong o che do chi doc.

## Cau truc thu muc

- `mysql/`: file cau hinh MySQL cho master/slave.
- `keepalived/`: file cau hinh VIP cho master/slave.
- `scripts/`: script monitor replication, health check keepalived.
- `tests/`: checklist test case can chay va ghi minh chung.

## Gia tri mau dang dung

### North
- Master: `192.168.1.10`
- Slave: `192.168.1.11`
- VIP: `192.168.1.100`
- VRID: `51`

### South
- Master: `192.168.2.10`
- Slave: `192.168.2.11`
- VIP: `192.168.2.100`
- VRID: `52`

## Thu tu trien khai nhanh

1. Chinh file trong `mysql/` cho dung server-id + subnet.
2. Enable replication cho North, test `SHOW SLAVE STATUS\G`.
3. Lam tuong tu cho South.
4. Cai `keepalived`, copy file trong `keepalived/`.
5. Gan script trong `scripts/`, cap quyen execute.
6. Chay checklist trong `tests/TEST_CHECKLIST.md`.

## Luu y quan trong

- Tuyet doi khong tat `read_only` va `super_read_only` tren slave.
- Backend nen ket noi vao VIP (khong ket noi truc tiep master/slave).
- Khi test failover, chup log va ket qua lenh de dua vao bao cao.
