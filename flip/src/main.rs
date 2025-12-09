use std::mem;
use std::ptr;
use winapi::shared::minwindef::DWORD;
use winapi::um::wingdi::{
    DEVMODEW, DISPLAY_DEVICEW, DM_PELSWIDTH, DM_PELSHEIGHT, DM_DISPLAYFREQUENCY,
};
use winapi::um::winuser::{
    ChangeDisplaySettingsExW, EnumDisplayDevicesW, EnumDisplaySettingsW,
    CDS_UPDATEREGISTRY, CDS_TEST, DISP_CHANGE_SUCCESSFUL, DISP_CHANGE_BADMODE,
    DISP_CHANGE_RESTART, ENUM_CURRENT_SETTINGS,
};

#[derive(Debug, Clone)]
pub struct MonitorInfo {
    pub device_name: String,
    pub device_string: String,
    pub current_width: u32,
    pub current_height: u32,
    pub current_frequency: u32,
    pub is_primary: bool,
}

#[derive(Debug)]
pub struct ResolutionChange {
    pub device_name: String,
    pub width: u32,
    pub height: u32,
    pub frequency: u32,
}

/// Convert a wide string (UTF-16) to a Rust String
fn wide_string_to_string(wide: &[u16]) -> String {
    let len = wide.iter().position(|&c| c == 0).unwrap_or(wide.len());
    String::from_utf16_lossy(&wide[..len])
}

/// Enumerate all monitors in the system
pub fn enumerate_monitors() -> Result<Vec<MonitorInfo>, String> {
    let mut monitors = Vec::new();
    let mut display_device: DISPLAY_DEVICEW = unsafe { mem::zeroed() };
    display_device.cb = mem::size_of::<DISPLAY_DEVICEW>() as DWORD;

    let mut device_num = 0;
    loop {
        let result = unsafe {
            EnumDisplayDevicesW(
                ptr::null(),
                device_num,
                &mut display_device,
                0,
            )
        };

        if result == 0 {
            break;
        }

        // Check if the device is attached to the desktop
        const DISPLAY_DEVICE_ATTACHED_TO_DESKTOP: DWORD = 0x00000001;
        const DISPLAY_DEVICE_PRIMARY_DEVICE: DWORD = 0x00000004;
        
        if display_device.StateFlags & DISPLAY_DEVICE_ATTACHED_TO_DESKTOP != 0 {
            let device_name = wide_string_to_string(&display_device.DeviceName);
            let device_string = wide_string_to_string(&display_device.DeviceString);
            let is_primary = display_device.StateFlags & DISPLAY_DEVICE_PRIMARY_DEVICE != 0;

            // Get current display settings
            let mut dev_mode: DEVMODEW = unsafe { mem::zeroed() };
            dev_mode.dmSize = mem::size_of::<DEVMODEW>() as u16;

            let device_name_wide: Vec<u16> = device_name
                .encode_utf16()
                .chain(std::iter::once(0))
                .collect();

            let settings_result = unsafe {
                EnumDisplaySettingsW(
                    device_name_wide.as_ptr(),
                    ENUM_CURRENT_SETTINGS,
                    &mut dev_mode,
                )
            };

            if settings_result != 0 {
                monitors.push(MonitorInfo {
                    device_name,
                    device_string,
                    current_width: dev_mode.dmPelsWidth,
                    current_height: dev_mode.dmPelsHeight,
                    current_frequency: dev_mode.dmDisplayFrequency,
                    is_primary,
                });
            }
        }

        device_num += 1;
    }

    if monitors.is_empty() {
        Err("No monitors found".to_string())
    } else {
        Ok(monitors)
    }
}

/// Change the display resolution for a specific monitor
pub fn change_resolution(change: &ResolutionChange) -> Result<String, String> {
    let mut dev_mode: DEVMODEW = unsafe { mem::zeroed() };
    dev_mode.dmSize = mem::size_of::<DEVMODEW>() as u16;

    let device_name_wide: Vec<u16> = change
        .device_name
        .encode_utf16()
        .chain(std::iter::once(0))
        .collect();

    // Get current settings first
    let result = unsafe {
        EnumDisplaySettingsW(
            device_name_wide.as_ptr(),
            ENUM_CURRENT_SETTINGS,
            &mut dev_mode,
        )
    };

    if result == 0 {
        return Err(format!("Failed to get current settings for {}", change.device_name));
    }

    // Modify the desired fields
    dev_mode.dmPelsWidth = change.width;
    dev_mode.dmPelsHeight = change.height;
    dev_mode.dmDisplayFrequency = change.frequency;
    dev_mode.dmFields = DM_PELSWIDTH | DM_PELSHEIGHT | DM_DISPLAYFREQUENCY;

    // Test the new settings first
    let test_result = unsafe {
        ChangeDisplaySettingsExW(
            device_name_wide.as_ptr(),
            &mut dev_mode,
            ptr::null_mut(),
            CDS_TEST,
            ptr::null_mut(),
        )
    };

    if test_result != DISP_CHANGE_SUCCESSFUL {
        return Err(format!(
            "Resolution {}x{}@{}Hz is not supported by {}. Error code: {}",
            change.width, change.height, change.frequency, change.device_name, test_result
        ));
    }

    // Apply the new settings
    let change_result = unsafe {
        ChangeDisplaySettingsExW(
            device_name_wide.as_ptr(),
            &mut dev_mode,
            ptr::null_mut(),
            CDS_UPDATEREGISTRY,
            ptr::null_mut(),
        )
    };

    match change_result {
        DISP_CHANGE_SUCCESSFUL => Ok(format!(
            "Successfully changed {} to {}x{}@{}Hz",
            change.device_name, change.width, change.height, change.frequency
        )),
        DISP_CHANGE_RESTART => Ok(format!(
            "Resolution changed. A restart may be required for full effect."
        )),
        DISP_CHANGE_BADMODE => Err(format!(
            "The requested mode is not supported by {}", change.device_name
        )),
        _ => Err(format!(
            "Failed to change resolution for {}. Error code: {}",
            change.device_name, change_result
        )),
    }
}

pub fn flip(monitor: &MonitorInfo, width: u32, height: u32, frequency: u32) -> Result<(), String> {
    let change = ResolutionChange {
        device_name: monitor.device_name.clone(),
        width,
        height,
        frequency,
    };

    println!("\nAttempting to change resolution...");
    change_resolution(&change)?;
    Ok(())
}

/// Interactive function to change resolution based on user input
pub fn interactive_resolution_change() -> Result<(), String> {
    println!("Scanning for monitors...\n");
    
    let monitors = enumerate_monitors()?;
    
    println!("Found {} monitor(s):\n", monitors.len());
    for (idx, monitor) in monitors.iter().enumerate() {
        println!("Monitor {}:", idx + 1);
        println!("  Device: {}", monitor.device_name);
        println!("  Description: {}", monitor.device_string);
        println!("  Current Resolution: {}x{}@{}Hz", 
                 monitor.current_width, monitor.current_height, monitor.current_frequency);
        println!("  Primary: {}\n", if monitor.is_primary { "Yes" } else { "No" });

        if monitor.current_width == 1920 && monitor.current_height == 1080 {
            return flip(monitor, 3840, 2160, monitor.current_frequency);
        } else if monitor.current_width == 3840 && monitor.current_height == 2160 {
            return flip(monitor, 1920, 1080, monitor.current_frequency);
        }
    }

    // Get user input
    println!("Enter monitor number to change (1-{}): ", monitors.len());
    let mut input = String::new();
    std::io::stdin().read_line(&mut input).map_err(|e| e.to_string())?;
    let monitor_idx: usize = input.trim().parse::<usize>().map_err(|_| "Invalid number")? - 1;

    if monitor_idx >= monitors.len() {
        return Err("Invalid monitor number".to_string());
    }

    println!("Enter new width (pixels): ");
    let mut input = String::new();
    std::io::stdin().read_line(&mut input).map_err(|e| e.to_string())?;
    let width: u32 = input.trim().parse().map_err(|_| "Invalid width")?;

    println!("Enter new height (pixels): ");
    let mut input = String::new();
    std::io::stdin().read_line(&mut input).map_err(|e| e.to_string())?;
    let height: u32 = input.trim().parse().map_err(|_| "Invalid height")?;

    println!("Enter refresh rate (Hz, typically 60 or 144): ");
    let mut input = String::new();
    std::io::stdin().read_line(&mut input).map_err(|e| e.to_string())?;
    let frequency: u32 = input.trim().parse().map_err(|_| "Invalid frequency")?;

    let change = ResolutionChange {
        device_name: monitors[monitor_idx].device_name.clone(),
        width,
        height,
        frequency,
    };

    println!("\nAttempting to change resolution...");
    let result = change_resolution(&change)?;
    println!("{}", result);

    Ok(())
}

fn main() {
    println!("=== Windows Multi-Monitor Resolution Changer ===\n");

    match interactive_resolution_change() {
        Ok(_) => println!("\nOperation completed successfully!"),
        Err(e) => eprintln!("\nError: {}", e),
    }
}
