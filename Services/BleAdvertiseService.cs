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

        // Log-only deterministic abbreviation; never returns the receiver ID itself.
        private static string? GetReceiverLogId()
        {
            try
            {
                string? receiverId = Program.Config?.receiver_device_id;
                if (string.IsNullOrWhiteSpace(receiverId))
                    return null;

                byte[] digest = System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(receiverId));
                return "h" + Convert.ToHexString(digest.AsSpan(0, 8));
            }
            catch
            {
                // Identifier formatting must not affect BLE processing.
                return null;
            }
        }

        public async void Start()
        {
            Logger.Trace("cs_ble_advertise", "BleAdvertiseService", "Start", "IN", "", businessId: Program.Config?.business_id, classId: Program.Config?.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: GetReceiverLogId());
            if (provider != null)
            {
                Logger.Trace("cs_ble_advertise", "BleAdvertiseService", "Start", "SKIP ALREADY STARTED", "", businessId: Program.Config?.business_id, classId: Program.Config?.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: GetReceiverLogId());
                return;
            }

            var result = await GattServiceProvider.CreateAsync(TaglessUuid);

            if (result.Error != BluetoothError.Success)
            {
                Logger.Trace("cs_ble_advertise", "BleAdvertiseService", "Start", "ERROR SERVICE CREATE", $"error={result.Error}", businessId: Program.Config?.business_id, classId: Program.Config?.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: GetReceiverLogId());
                return;
            }

            Logger.Trace("cs_ble_advertise", "BleAdvertiseService", "Start", "SERVICE CREATED", "", businessId: Program.Config?.business_id, classId: Program.Config?.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: GetReceiverLogId());
            provider = result.ServiceProvider;
            int lastLoggedAdvertisementStatus = -1;

            provider.AdvertisementStatusChanged += (sender, args) =>
            {
                if (System.Threading.Interlocked.Exchange(ref lastLoggedAdvertisementStatus, (int)args.Status) != (int)args.Status)
                {
                    Logger.Trace("cs_ble_advertise", "BleAdvertiseService", "AdvertisementStatusChanged", "STATUS", $"status={args.Status}, error={args.Error}", businessId: Program.Config?.business_id, classId: Program.Config?.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: GetReceiverLogId());
                }
            };

            // TRE config.json의 핵심 정보를 TSI가 읽을 수 있도록
            // TRE_CONFIG Characteristic을 생성한다.
            var config = tagless_receiver.Program.Config;

            if (config == null)
            {
                Logger.Trace("cs_ble_advertise", "BleAdvertiseService", "Start", "CONFIG MISSING", "", businessId: Program.Config?.business_id, classId: Program.Config?.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: GetReceiverLogId());
                return;
            }

            var parameters = new GattLocalCharacteristicParameters
            {
                CharacteristicProperties = GattCharacteristicProperties.Read,
                ReadProtectionLevel = GattProtectionLevel.Plain
            };

            var characteristicResult =
                await provider.Service.CreateCharacteristicAsync(
                    TreConfigUuid,
                    parameters
                );

            if (characteristicResult.Error != BluetoothError.Success)
            {
                Logger.Trace("cs_ble_advertise", "BleAdvertiseService", "Start", "ERROR CHARACTERISTIC CREATE", $"error={characteristicResult.Error}", businessId: Program.Config?.business_id, classId: Program.Config?.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: GetReceiverLogId());
                return;
            }

            Logger.Trace("cs_ble_advertise", "BleAdvertiseService", "Start", "CHARACTERISTIC CREATED", "", businessId: Program.Config?.business_id, classId: Program.Config?.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: GetReceiverLogId());
            characteristicResult.Characteristic.ReadRequested += async (sender, args) =>
            {
                Logger.Trace("cs_ble_advertise", "BleAdvertiseService", "ReadRequested", "READ REQUEST", "", businessId: Program.Config?.business_id, classId: Program.Config?.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: GetReceiverLogId());
                var deferral = args.GetDeferral();
                try
                {
                    var request = await args.GetRequestAsync();
                    if (request is null)
                    {
                        Logger.Trace("cs_ble_advertise", "BleAdvertiseService", "ReadRequested", "READ REQUEST NULL", "", businessId: Program.Config?.business_id, classId: Program.Config?.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: GetReceiverLogId());
                        return;
                    }
                    var current = tagless_receiver.Program.Config;
                    string treConfig = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        current.receiver_device_id,
                        current.business_id,
                        current.class_id
                    });
                    byte[] bytes = Encoding.UTF8.GetBytes(treConfig);
                    if (request.Offset > bytes.Length)
                    {
                        request.RespondWithProtocolError(0x07);
                        Logger.Trace("cs_ble_advertise", "BleAdvertiseService", "ReadRequested", "READ OFFSET ERROR", "protocol_error=0x07", businessId: Program.Config?.business_id, classId: Program.Config?.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: GetReceiverLogId());
                        return;
                    }
                    // 긴 GATT 값을 읽는 클라이언트의 offset도 처리한다.
                    using var writer = new DataWriter();
                    writer.WriteBytes(bytes.Skip((int)request.Offset).ToArray());
                    request.RespondWithValue(writer.DetachBuffer());
                    Logger.Trace("cs_ble_advertise", "BleAdvertiseService", "ReadRequested", "READ RESPONDED", "", businessId: Program.Config?.business_id, classId: Program.Config?.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: GetReceiverLogId());
                }
                catch (Exception ex) { Logger.Trace("cs_ble_advertise", "BleAdvertiseService", "ReadRequested", "ERROR READ", $"type={ex.GetType().Name}", businessId: Program.Config?.business_id, classId: Program.Config?.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: GetReceiverLogId()); }
                finally { deferral.Complete(); }
            };
            Logger.Trace("cs_ble_advertise", "BleAdvertiseService", "Start", "CONFIG READ READY", "", businessId: Program.Config?.business_id, classId: Program.Config?.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: GetReceiverLogId());


            try
            {
                provider.StartAdvertising(
                    new GattServiceProviderAdvertisingParameters
                    {
                        IsDiscoverable = true,
                        IsConnectable = true
                    }
                );

                Logger.Trace("cs_ble_advertise", "BleAdvertiseService", "Start", "ADVERTISE REQUESTED", "", businessId: Program.Config?.business_id, classId: Program.Config?.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: GetReceiverLogId());
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                Logger.Trace("cs_ble_advertise", "BleAdvertiseService", "Start", "ERROR", $"type={ex.GetType().Name}, HRESULT=0x{ex.HResult:X8}", businessId: Program.Config?.business_id, classId: Program.Config?.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: GetReceiverLogId());
            }
            catch (Exception ex)
            {
                Logger.Trace("cs_ble_advertise", "BleAdvertiseService", "Start", "ERROR", $"type={ex.GetType().Name}", businessId: Program.Config?.business_id, classId: Program.Config?.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: GetReceiverLogId());
            }

        }

        public void Stop()
        {
            Logger.Trace("cs_ble_advertise", "BleAdvertiseService", "Stop", "IN", "", businessId: Program.Config?.business_id, classId: Program.Config?.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: GetReceiverLogId());
            if (provider == null)
            {
                Logger.Trace("cs_ble_advertise", "BleAdvertiseService", "Stop", "SKIP NOT STARTED", "", businessId: Program.Config?.business_id, classId: Program.Config?.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: GetReceiverLogId());
                return;
            }

            provider.StopAdvertising();
            Logger.Trace("cs_ble_advertise", "BleAdvertiseService", "Stop", "ADVERTISE STOPPED", "", businessId: Program.Config?.business_id, classId: Program.Config?.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: GetReceiverLogId());
            provider = null;

            Logger.Trace("cs_ble_advertise", "BleAdvertiseService", "Stop", "OUT", "", businessId: Program.Config?.business_id, classId: Program.Config?.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: GetReceiverLogId());
        }
    }
}