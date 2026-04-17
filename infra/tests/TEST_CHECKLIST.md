# Test Checklist - Nhi + Tri

## TC3 - Replication hoat dong
- [ ] Tren North master, tao 1 ban ghi moi (vi du insert vao `Trips`).
- [ ] Trong <= 1 giay, ban ghi xuat hien tren North slave.
- [ ] Lap lai tuong tu voi South.

Minh chung can chup:
- `SHOW SLAVE STATUS\G` (co `Slave_IO_Running: Yes`, `Slave_SQL_Running: Yes`)
- `Seconds_Behind_Master <= 1`

## TC4 - Failover North (master down)
- [ ] Tat MySQL hoac tat may North master.
- [ ] Kiem tra VIP North chuyen sang North slave (`ip a`).
- [ ] Thu API GET (doc) => thanh cong.
- [ ] Thu API POST/PUT (ghi) => that bai do read-only.

## TC5 - Failover South (master down)
- [ ] Lam tuong tu TC4 cho South.

## TC6 - Hai vung doc lap
- [ ] North master down, South van on.
- [ ] App/Backend mien South van ghi duoc.
- [ ] App/Backend mien North chi doc.

## TC7 - Khoi phuc primary
- [ ] Bat lai North master.
- [ ] Kiem tra replication catch-up.
- [ ] Tra VIP ve master (neu dung chinh sach failback) hoac giu theo cau hinh.
- [ ] Xac nhan he thong tro lai che do binh thuong.
