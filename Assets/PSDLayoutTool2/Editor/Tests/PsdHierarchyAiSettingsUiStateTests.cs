namespace PsdLayoutTool2.Tests
{
    using NUnit.Framework;

    public sealed class PsdHierarchyAiSettingsUiStateTests
    {
        [Test]
        public void DefaultModeHidesCustomConnectionControls()
        {
            PsdHierarchyAiSettingsUiState state = PsdHierarchyAiSettingsUiState.Resolve(
                PsdHierarchyAiConnectionMode.Default,
                string.Empty,
                false,
                false,
                false,
                true);

            Assert.That(state.showBaseUrl, Is.False);
            Assert.That(state.showApiKey, Is.False);
            Assert.That(state.showRevealKey, Is.False);
            Assert.That(state.showTestConnection, Is.False);
            Assert.That(state.testConnectionEnabled, Is.False);
        }

        [Test]
        public void CustomModeShowsFieldsButRequiresValidUrlAndSavedKeyForTesting()
        {
            PsdHierarchyAiSettingsUiState state = PsdHierarchyAiSettingsUiState.Resolve(
                PsdHierarchyAiConnectionMode.Custom,
                "https://api.example.com/v1",
                false,
                false,
                false,
                true);

            Assert.That(state.showBaseUrl, Is.True);
            Assert.That(state.showApiKey, Is.True);
            Assert.That(state.showRevealKey, Is.True);
            Assert.That(state.showTestConnection, Is.True);
            Assert.That(state.testConnectionEnabled, Is.False);
            Assert.That(state.credentialState, Is.EqualTo(PsdHierarchyAiCredentialState.Missing));
        }

        [Test]
        public void ValidCustomConnectionWithSavedKeyEnablesTesting()
        {
            PsdHierarchyAiSettingsUiState state = PsdHierarchyAiSettingsUiState.Resolve(
                PsdHierarchyAiConnectionMode.Custom,
                "https://api.example.com/v1",
                true,
                false,
                false,
                true);

            Assert.That(state.testConnectionEnabled, Is.True);
            Assert.That(state.baseUrlError, Is.Empty);
            Assert.That(state.credentialState, Is.EqualTo(PsdHierarchyAiCredentialState.Saved));
        }

        [Test]
        public void InvalidCustomUrlDisablesTestingAndExposesValidationMessage()
        {
            PsdHierarchyAiSettingsUiState state = PsdHierarchyAiSettingsUiState.Resolve(
                PsdHierarchyAiConnectionMode.Custom,
                "http://api.example.com/v1",
                true,
                false,
                false,
                true);

            Assert.That(state.testConnectionEnabled, Is.False);
            Assert.That(state.baseUrlError, Is.EqualTo(
                "API base URL must use HTTPS, except for loopback HTTP endpoints."));
        }

        [Test]
        public void MissingCustomUrlUsesTheRequiredFieldMessage()
        {
            PsdHierarchyAiSettingsUiState state = PsdHierarchyAiSettingsUiState.Resolve(
                PsdHierarchyAiConnectionMode.Custom,
                string.Empty,
                true,
                false,
                false,
                true);

            Assert.That(state.testConnectionEnabled, Is.False);
            Assert.That(state.baseUrlError, Is.EqualTo("API base URL is required."));
        }

        [Test]
        public void ReplacementAndClearStatesAreExplicitAndDisableTesting()
        {
            PsdHierarchyAiSettingsUiState replacement = PsdHierarchyAiSettingsUiState.Resolve(
                PsdHierarchyAiConnectionMode.Custom,
                "https://api.example.com/v1",
                true,
                true,
                false,
                true);
            PsdHierarchyAiSettingsUiState clear = PsdHierarchyAiSettingsUiState.Resolve(
                PsdHierarchyAiConnectionMode.Custom,
                "https://api.example.com/v1",
                true,
                false,
                true,
                true);

            Assert.That(replacement.credentialState, Is.EqualTo(PsdHierarchyAiCredentialState.ReplacementPending));
            Assert.That(replacement.testConnectionEnabled, Is.False);
            Assert.That(clear.credentialState, Is.EqualTo(PsdHierarchyAiCredentialState.ClearPending));
            Assert.That(clear.testConnectionEnabled, Is.False);
        }

        [Test]
        public void UnavailableSecretStoreDisablesCredentialAndTestActionsWithErrorState()
        {
            PsdHierarchyAiSettingsUiState state = PsdHierarchyAiSettingsUiState.Resolve(
                PsdHierarchyAiConnectionMode.Custom,
                "https://api.example.com/v1",
                false,
                false,
                false,
                false);

            Assert.That(state.secretStoreAvailable, Is.False);
            Assert.That(state.credentialActionsEnabled, Is.False);
            Assert.That(state.testConnectionEnabled, Is.False);
            Assert.That(state.statusSeverity, Is.EqualTo(PsdHierarchyAiSettingsStatusSeverity.Error));
        }
    }
}
