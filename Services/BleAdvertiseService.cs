using System;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;

namespace tagless_receiver.Services
{
    internal class BleAdvertiseService
    {
        private BluetoothLEAdvertisementPublisher? publisher;

        private static readonly Guid TaglessUuid =
            Guid.Parse("e747f937-5029-55a4-90ed-370194b05a34");

        public void Start()
        {
            if (publisher != null)
                return;

            publisher = new BluetoothLEAdvertisementPublisher();

            var data = new BluetoothLEManufacturerData
            {
                CompanyId = 0xFFFE
            };

            var writer = new DataWriter();
            writer.WriteBytes(TaglessUuid.ToByteArray());
            data.Data = writer.DetachBuffer();

            publisher.Advertisement.ManufacturerData.Add(data);

            publisher.StatusChanged += (sender, args) =>
            {
                Logger.Write(
                    $"[BLE-TX] status={args.Status}, UUID={TaglessUuid}"
                );
            };

            publisher.Start();

            Logger.Write(
                $"[BLE-TX] 광고 시작 UUID={TaglessUuid}"
            );
        }

        public void Stop()
        {
            if (publisher == null)
                return;

            publisher.Stop();
            publisher = null;

            Logger.Write("[BLE-TX] 광고 중지");
        }
    }
}