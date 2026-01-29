using System;
using InTheHand.Net.Sockets;
using InTheHand.Net.Bluetooth;
using WiimoteLib;

namespace TheGround.PoC.BalanceBoard;

/// <summary>
/// Handles Bluetooth pairing and HID setup for Wii Balance Board.
/// Uses 32Feet.NET for direct Bluetooth control.
/// </summary>
public class BluetoothSetup
{
    public event Action<string>? OnStatusChanged;

    /// <summary>
    /// Searches for and sets up Nintendo Balance Board devices.
    /// This enables the HID service which is required for WiimoteLib to detect them.
    /// </summary>
    public bool SetupBalanceBoard()
    {
        try
        {
            using var btClient = new BluetoothClient();
            
            OnStatusChanged?.Invoke("Searching for Bluetooth devices...");
            
            // Find remembered (paired) devices
            var rememberedDevices = btClient.DiscoverDevices(255, false, true, false);
            int nintendoDevicesFound = 0;
            
            foreach (var device in rememberedDevices)
            {
                if (!device.DeviceName.Contains("Nintendo")) continue;
                
                nintendoDevicesFound++;
                OnStatusChanged?.Invoke($"Found: {device.DeviceName} - Enabling HID service...");
                
                try
                {
                    // This is the key step - enable HID service
                    device.SetServiceState(BluetoothService.HumanInterfaceDevice, true);
                    System.Threading.Thread.Sleep(1000);
                }
                catch (Exception ex)
                {
                    OnStatusChanged?.Invoke($"HID enable failed: {ex.Message}");
                }
            }
            
            if (nintendoDevicesFound == 0)
            {
                // Try to find unknown (unpaired) devices
                OnStatusChanged?.Invoke("No paired Nintendo devices. Scanning for new devices...");
                var unknownDevices = btClient.DiscoverDevices(255, false, false, true);
                
                foreach (var device in unknownDevices)
                {
                    if (!device.DeviceName.Contains("Nintendo")) continue;
                    
                    nintendoDevicesFound++;
                    OnStatusChanged?.Invoke($"Found new device: {device.DeviceName} - Pairing...");
                    
                    try
                    {
                        // Pair without PIN
                        BluetoothSecurity.PairRequest(device.DeviceAddress, null);
                        System.Threading.Thread.Sleep(2000);
                        
                        // Enable HID service
                        device.SetServiceState(BluetoothService.HumanInterfaceDevice, true);
                        System.Threading.Thread.Sleep(1000);
                    }
                    catch (Exception ex)
                    {
                        OnStatusChanged?.Invoke($"Pairing failed: {ex.Message}");
                    }
                }
            }
            
            if (nintendoDevicesFound > 0)
            {
                // Wait for driver installation
                OnStatusChanged?.Invoke("Waiting for driver installation...");
                System.Threading.Thread.Sleep(3000);
                
                // Try to wake up the device
                try
                {
                    var collection = new WiimoteCollection();
                    collection.FindAllWiimotes();
                    
                    if (collection.Count > 0)
                    {
                        foreach (var wii in collection)
                        {
                            wii.Connect();
                            wii.SetLEDs(true, false, false, false);
                            wii.Disconnect();
                        }
                        OnStatusChanged?.Invoke($"Success! Found {collection.Count} device(s). Click Connect.");
                        return true;
                    }
                }
                catch { }
                
                OnStatusChanged?.Invoke("Device found but not yet in HID list. Try again or reconnect Bluetooth.");
                return false;
            }
            
            OnStatusChanged?.Invoke("No Nintendo devices found. Press SYNC button on Balance Board.");
            return false;
        }
        catch (Exception ex)
        {
            OnStatusChanged?.Invoke($"Bluetooth error: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Removes existing Nintendo Bluetooth devices (for clean re-pairing).
    /// </summary>
    public void RemoveNintendoDevices()
    {
        try
        {
            using var btClient = new BluetoothClient();
            var devices = btClient.DiscoverDevices(255, false, true, false);
            int removed = 0;
            
            foreach (var device in devices)
            {
                if (!device.DeviceName.Contains("Nintendo")) continue;
                
                OnStatusChanged?.Invoke($"Removing: {device.DeviceName}");
                BluetoothSecurity.RemoveDevice(device.DeviceAddress);
                device.SetServiceState(BluetoothService.HumanInterfaceDevice, false);
                removed++;
            }
            
            OnStatusChanged?.Invoke($"Removed {removed} device(s).");
        }
        catch (Exception ex)
        {
            OnStatusChanged?.Invoke($"Error: {ex.Message}");
        }
    }
}
