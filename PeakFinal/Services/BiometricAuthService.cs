namespace Peak;

public sealed record BiometricAvailability(bool IsAvailable, string Message);
public sealed record BiometricAuthResult(bool IsSuccess, bool WasCancelled, string Message);

public static class BiometricAuthService
{
    public static async Task<BiometricAvailability> GetAvailabilityAsync()
    {
        try
        {
#if ANDROID
            return GetAndroidAvailability();
#elif IOS || MACCATALYST
            return GetAppleAvailability();
#elif WINDOWS
            return await GetWindowsAvailabilityAsync();
#else
            return new BiometricAvailability(false, "Biometric login is not supported on this platform.");
#endif
        }
        catch (Exception ex)
        {
            return new BiometricAvailability(false, $"Biometric check failed: {ex.Message}");
        }
    }

    public static async Task<BiometricAuthResult> AuthenticateAsync(string reason)
    {
        var availability = await GetAvailabilityAsync();
        if (!availability.IsAvailable)
        {
            return new BiometricAuthResult(false, false, availability.Message);
        }

        try
        {
#if ANDROID
            return await AuthenticateOnAndroidAsync(reason);
#elif IOS || MACCATALYST
            return await AuthenticateOnAppleAsync(reason);
#elif WINDOWS
            return await AuthenticateOnWindowsAsync(reason);
#else
            return new BiometricAuthResult(false, false, "Biometric login is not supported on this platform.");
#endif
        }
        catch (Exception ex)
        {
            return new BiometricAuthResult(false, false, $"Biometric authentication failed: {ex.Message}");
        }
    }

#if ANDROID
    private static BiometricAvailability GetAndroidAvailability()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(28))
        {
            return new BiometricAvailability(false, "Fingerprint login requires Android 9 or newer.");
        }

        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        if (activity is null)
        {
            return new BiometricAvailability(false, "Unable to access the active screen for fingerprint authentication.");
        }

        var fingerprintManager = activity.GetSystemService(Android.Content.Context.FingerprintService)
            as Android.Hardware.Fingerprints.FingerprintManager;
        if (fingerprintManager is null)
        {
            return new BiometricAvailability(false, "This device does not expose fingerprint services.");
        }

        if (!fingerprintManager.IsHardwareDetected)
        {
            return new BiometricAvailability(false, "This device has no fingerprint hardware.");
        }

        if (!fingerprintManager.HasEnrolledFingerprints)
        {
            return new BiometricAvailability(false, "No fingerprint is enrolled on this device yet.");
        }

        var keyguardManager = activity.GetSystemService(Android.Content.Context.KeyguardService) as Android.App.KeyguardManager;
        if (keyguardManager is not null && !keyguardManager.IsKeyguardSecure)
        {
            return new BiometricAvailability(false, "Set up a screen lock first to enable fingerprint login.");
        }

        return new BiometricAvailability(true, "Fingerprint authentication is available.");
    }

    private static Task<BiometricAuthResult> AuthenticateOnAndroidAsync(string reason)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(28))
        {
            return Task.FromResult(new BiometricAuthResult(
                false,
                false,
                "Fingerprint login requires Android 9 or newer."));
        }

        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        if (activity is null)
        {
            return Task.FromResult(new BiometricAuthResult(
                false,
                false,
                "Unable to access the active screen for fingerprint authentication."));
        }

        var tcs = new TaskCompletionSource<BiometricAuthResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var executor = activity.MainExecutor;
        if (executor is null)
        {
            return Task.FromResult(new BiometricAuthResult(
                false,
                false,
                "Unable to initialize fingerprint authentication."));
        }

        var callback = new AndroidBiometricCallback(tcs);
        var cancelSignal = new Android.OS.CancellationSignal();

        var prompt = new Android.Hardware.Biometrics.BiometricPrompt.Builder(activity)
            .SetTitle("Fingerprint verification")
            .SetSubtitle(reason)
            .SetNegativeButton("Cancel", executor, new AndroidNegativeButtonHandler(tcs))
            .Build();

        prompt.Authenticate(cancelSignal, executor, callback);
        return tcs.Task;
    }

    private sealed class AndroidBiometricCallback : Android.Hardware.Biometrics.BiometricPrompt.AuthenticationCallback
    {
        private readonly TaskCompletionSource<BiometricAuthResult> _tcs;

        public AndroidBiometricCallback(TaskCompletionSource<BiometricAuthResult> tcs)
        {
            _tcs = tcs;
        }

        public override void OnAuthenticationSucceeded(Android.Hardware.Biometrics.BiometricPrompt.AuthenticationResult? result)
        {
            base.OnAuthenticationSucceeded(result);
            _tcs.TrySetResult(new BiometricAuthResult(true, false, "Fingerprint verified."));
        }

        public override void OnAuthenticationError(
            Android.Hardware.Biometrics.BiometricErrorCode errorCode,
            Java.Lang.ICharSequence? errString)
        {
            base.OnAuthenticationError(errorCode, errString);

            var code = (int)errorCode;
            var wasCancelled = code is 5 or 10 or 13;

            var message = errString?.ToString() ?? "Fingerprint authentication failed.";
            _tcs.TrySetResult(new BiometricAuthResult(false, wasCancelled, message));
        }
    }

    private sealed class AndroidNegativeButtonHandler : Java.Lang.Object, Android.Content.IDialogInterfaceOnClickListener
    {
        private readonly TaskCompletionSource<BiometricAuthResult> _tcs;

        public AndroidNegativeButtonHandler(TaskCompletionSource<BiometricAuthResult> tcs)
        {
            _tcs = tcs;
        }

        public void OnClick(Android.Content.IDialogInterface? dialog, int which)
        {
            _tcs.TrySetResult(new BiometricAuthResult(false, true, "Verification cancelled."));
        }
    }
#endif

#if IOS || MACCATALYST
    private static BiometricAvailability GetAppleAvailability()
    {
        var context = new LocalAuthentication.LAContext();
        if (context.CanEvaluatePolicy(LocalAuthentication.LAPolicy.DeviceOwnerAuthenticationWithBiometrics, out var error))
        {
            return new BiometricAvailability(true, "Biometric authentication is available.");
        }

        var code = (LocalAuthentication.LAStatus)(error?.Code ?? 0);
        return code switch
        {
            LocalAuthentication.LAStatus.BiometryNotEnrolled
                => new BiometricAvailability(false, "No fingerprint or Face ID is enrolled on this device."),
            LocalAuthentication.LAStatus.BiometryNotAvailable
                => new BiometricAvailability(false, "Biometric authentication is not available on this device."),
            LocalAuthentication.LAStatus.BiometryLockout
                => new BiometricAvailability(false, "Biometric authentication is locked. Unlock your device first."),
            _ => new BiometricAvailability(false, "Biometric authentication is currently unavailable.")
        };
    }

    private static Task<BiometricAuthResult> AuthenticateOnAppleAsync(string reason)
    {
        var context = new LocalAuthentication.LAContext();
        var tcs = new TaskCompletionSource<BiometricAuthResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        context.EvaluatePolicy(
            LocalAuthentication.LAPolicy.DeviceOwnerAuthenticationWithBiometrics,
            reason,
            (success, error) =>
            {
                if (success)
                {
                    tcs.TrySetResult(new BiometricAuthResult(true, false, "Biometric verification succeeded."));
                    return;
                }

                var code = (LocalAuthentication.LAStatus)(error?.Code ?? 0);
                var wasCancelled =
                    code == LocalAuthentication.LAStatus.UserCancel ||
                    code == LocalAuthentication.LAStatus.SystemCancel ||
                    code == LocalAuthentication.LAStatus.AppCancel;

                var message = error?.LocalizedDescription ?? "Biometric verification failed.";
                tcs.TrySetResult(new BiometricAuthResult(false, wasCancelled, message));
            });

        return tcs.Task;
    }
#endif

#if WINDOWS
    private static async Task<BiometricAvailability> GetWindowsAvailabilityAsync()
    {
        var availability = await Windows.Security.Credentials.UI.UserConsentVerifier.CheckAvailabilityAsync();
        return availability switch
        {
            Windows.Security.Credentials.UI.UserConsentVerifierAvailability.Available
                => new BiometricAvailability(true, "Windows Hello is available."),
            Windows.Security.Credentials.UI.UserConsentVerifierAvailability.DeviceNotPresent
                => new BiometricAvailability(false, "No biometric device is present."),
            Windows.Security.Credentials.UI.UserConsentVerifierAvailability.NotConfiguredForUser
                => new BiometricAvailability(false, "Windows Hello is not configured for this user."),
            Windows.Security.Credentials.UI.UserConsentVerifierAvailability.DisabledByPolicy
                => new BiometricAvailability(false, "Windows Hello is disabled by policy."),
            Windows.Security.Credentials.UI.UserConsentVerifierAvailability.DeviceBusy
                => new BiometricAvailability(false, "The biometric device is busy right now."),
            _ => new BiometricAvailability(false, "Windows Hello is unavailable.")
        };
    }

    private static async Task<BiometricAuthResult> AuthenticateOnWindowsAsync(string reason)
    {
        var result = await Windows.Security.Credentials.UI.UserConsentVerifier.RequestVerificationAsync(reason);
        return result switch
        {
            Windows.Security.Credentials.UI.UserConsentVerificationResult.Verified
                => new BiometricAuthResult(true, false, "Windows Hello verification succeeded."),
            Windows.Security.Credentials.UI.UserConsentVerificationResult.Canceled
                => new BiometricAuthResult(false, true, "Verification cancelled."),
            Windows.Security.Credentials.UI.UserConsentVerificationResult.DeviceBusy
                => new BiometricAuthResult(false, false, "The biometric device is busy. Try again."),
            Windows.Security.Credentials.UI.UserConsentVerificationResult.RetriesExhausted
                => new BiometricAuthResult(false, false, "Too many failed attempts. Try again later."),
            Windows.Security.Credentials.UI.UserConsentVerificationResult.DeviceNotPresent
                => new BiometricAuthResult(false, false, "No biometric device is present."),
            Windows.Security.Credentials.UI.UserConsentVerificationResult.DisabledByPolicy
                => new BiometricAuthResult(false, false, "Biometric verification is disabled by policy."),
            Windows.Security.Credentials.UI.UserConsentVerificationResult.NotConfiguredForUser
                => new BiometricAuthResult(false, false, "Biometric verification is not configured for this user."),
            _ => new BiometricAuthResult(false, false, "Biometric verification failed.")
        };
    }
#endif
}
