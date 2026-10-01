// ============================================================================
// BleAdvertiseService.cs = TRE_BB  쏘는 놈. TRE 는 받는 놈. 
//
// 목적:
//   Windows TRE가 Tagless UUID를 "실제 GATT Service UUID"로 BLE 광고한다.
//
// 배경:
//   기존 방식은 Manufacturer Data(CompanyId 0xFFFE)에
//   Tagless UUID를 넣어 광고했다.
//   이 방식은 iPhone 화면 ON 상태에서는 정상 수신되지만,
//   iPhone 화면 OFF 상태의 background scan에서는 사용할 수 없었다.
//
// 변경 이유:
//   iOS의 일반 background BLE scan은 특정 Service UUID를 지정하여
//   scanForPeripherals(withServices:) 하는 방식이 필요하다.
//
//   Windows BluetoothLEAdvertisementPublisher에서
//   Advertisement.ServiceUuids.Add()로 UUID를 직접 넣으려 했으나
//   ArgumentException이 발생했다.
//
// 새 방식:
//   GattServiceProvider로 Tagless UUID의 실제 GATT Service를 생성하고,
//   Windows가 그 Service UUID를 BLE Advertisement에 넣도록 한다.
//
// Tagless Service UUID:
//   e747f937-5029-55a4-90ed-370194b05a34
//
// 시험 목표:
//   1. Windows TRE에서 Service UUID 광고 성공
//   2. iPhone TSI에서 해당 Service UUID 발견
//   3. 최종적으로 iPhone 화면 OFF 상태에서도 didDiscover 발생 확인
//
// 주의:
//   이 파일은 GATT Service UUID 광고 시험용.
//   기존 Manufacturer Data 방식은
//   BleAdvertiseService_manufacturer.cs 로 백업.
// ============================================================================
using System;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using System.Text;
using Windows.Storage.Streams;

namespace tagless_receiver.Services
{
    internal class BleAdvertiseService
    {
        private GattServiceProvider? provider;

        private static readonly Guid TaglessUuid =
            Guid.Parse("e747f937-5029-55a4-90ed-370194b05a34");

        private static readonly Guid TreConfigUuid = 
            Guid.Parse("e747f937-5029-55a4-90ed-370194b05a35"); 

        public async void Start()
        {
            if (provider != null)
                return;

            var result = await GattServiceProvider.CreateAsync(TaglessUuid);

            if (result.Error != BluetoothError.Success)
            {
                Logger.Write($"[BLE-GATT] Service 생성 실패: {result.Error}");
                return;
            }

            provider = result.ServiceProvider;

            provider.AdvertisementStatusChanged += (sender, args) =>
            {
                Logger.Write($"[BLE-GATT] status={args.Status}, UUID={TaglessUuid}");
            };

            // TRE config.json의 핵심 정보를 TSI가 읽을 수 있도록
            // TRE_CONFIG Characteristic을 생성한다.
            var config = tagless_receiver.Program.Config;

            if (config == null)
            {
                Logger.Write("[BLE-GATT] config 없음");
                return;
            }

            string treConfig =
                $"{{\"receiver_device_id\":\"{config.receiver_device_id}\"," +
                $"\"business_id\":{config.business_id}," +
                $"\"class_id\":{config.class_id}}}";

            var writer = new DataWriter();
            writer.WriteBytes(Encoding.UTF8.GetBytes(treConfig));

            var parameters = new GattLocalCharacteristicParameters
            {
                CharacteristicProperties = GattCharacteristicProperties.Read,
                ReadProtectionLevel = GattProtectionLevel.Plain,
                StaticValue = writer.DetachBuffer()
            };

            var characteristicResult =
                await provider.Service.CreateCharacteristicAsync(
                    TreConfigUuid,
                    parameters
                );

            if (characteristicResult.Error != BluetoothError.Success)
            {
                Logger.Write(
                    $"[BLE-GATT] TRE_CONFIG 생성 실패: {characteristicResult.Error}"
                );
                return;
            }

            Logger.Write($"[BLE-GATT] TRE_CONFIG 생성: {treConfig}");

            provider.StartAdvertising(
                new GattServiceProviderAdvertisingParameters
                {
                    IsDiscoverable = true,
                    IsConnectable = true
                }
            );

            Logger.Write($"[BLE-GATT] 광고 시작 UUID={TaglessUuid}");
        }

        public void Stop()
        {
            if (provider == null)
                return;

            provider.StopAdvertising();
            provider = null;

            Logger.Write("[BLE-GATT] 광고 중지");
        }
    }
}