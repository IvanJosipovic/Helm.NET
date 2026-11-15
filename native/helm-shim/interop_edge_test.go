package main

import (
	"crypto/ecdsa"
	"crypto/elliptic"
	"crypto/rand"
	"crypto/x509"
	"crypto/x509/pkix"
	"encoding/json"
	"encoding/pem"
	"math/big"
	"os"
	"path/filepath"
	"reflect"
	"strings"
	"testing"
	"time"

	"helm.sh/helm/v4/pkg/action"
	releasev1 "helm.sh/helm/v4/pkg/release/v1"
)

func TestNativeReleaseFromViewValidatesAndMapsOptionalData(t *testing.T) {
	started := time.Date(2026, time.January, 2, 3, 4, 5, 123456789, time.UTC)
	completed := started.Add(time.Second)
	view := map[string]any{
		"name": "web", "namespace": "apps", "revision": int64(4), "updated": started,
		"status": "deployed", "chart": "nginx-1.2.3", "resources": map[string]any{"pods": []string{"web"}},
		"hooks": []*releasev1.Hook{
			nil,
			{
				Name: "smoke", Kind: "Pod", Path: "templates/hook.yaml", Manifest: "kind: Pod",
				Events: []releasev1.HookEvent{releasev1.HookTest},
				LastRun: releasev1.HookExecution{
					StartedAt: started, CompletedAt: completed, Phase: releasev1.HookPhase("Succeeded"),
				},
				Weight: 4, DeletePolicies: []releasev1.HookDeletePolicy{releasev1.HookSucceeded},
			},
			{Name: "pending"},
		},
	}

	got, ok := nativeReleaseFromView(view)
	if !ok {
		t.Fatal("expected a valid Helm release view")
	}
	if got.Name != "web" || got.Namespace != "apps" || got.Revision != 4 || got.Updated != started.Format(time.RFC3339Nano) {
		t.Fatalf("release identity fields changed: %#v", got)
	}
	var resources map[string]any
	if err := json.Unmarshal([]byte(got.ResourcesJSON), &resources); err != nil || len(resources) != 1 {
		t.Fatalf("release resources were not serialized: %q, %v", got.ResourcesJSON, err)
	}
	if len(got.Hooks) != 2 || got.Hooks[0].Name != "smoke" || got.Hooks[0].StartedAt != started.Format(time.RFC3339Nano) || got.Hooks[0].CompletedAt != completed.Format(time.RFC3339Nano) || got.Hooks[0].Phase != "Succeeded" || got.Hooks[0].Weight != 4 || !reflect.DeepEqual(got.Hooks[0].Events, []string{"test"}) || !reflect.DeepEqual(got.Hooks[0].DeletePolicies, []string{"hook-succeeded"}) {
		t.Fatalf("hook details were not mapped: %#v", got.Hooks)
	}
	if got.Hooks[1].StartedAt != "" || got.Hooks[1].CompletedAt != "" {
		t.Fatalf("zero hook timestamps were not omitted: %#v", got.Hooks[1])
	}
}

func TestNativeReleaseFromViewRejectsInvalidRequiredFields(t *testing.T) {
	validTime := time.Date(2026, time.January, 2, 3, 4, 5, 0, time.UTC)
	for _, test := range []struct {
		name string
		data any
	}{
		{name: "wrong value type", data: "not a release"},
		{name: "missing timestamp", data: map[string]any{"revision": 1}},
		{name: "wrong timestamp type", data: map[string]any{"updated": "2026-01-02T03:04:05Z", "revision": 1}},
		{name: "wrong revision type", data: map[string]any{"updated": validTime, "revision": "1"}},
		{name: "unencodable resources", data: map[string]any{"updated": validTime, "revision": 1, "resources": make(chan int)}},
	} {
		t.Run(test.name, func(t *testing.T) {
			if _, ok := nativeReleaseFromView(test.data); ok {
				t.Fatalf("expected invalid release view to be rejected: %#v", test.data)
			}
		})
	}
}

func TestIntegerValueAcceptsHelmRevisionIntegerTypes(t *testing.T) {
	for _, test := range []struct {
		value any
		want  int
	}{
		{value: int(3), want: 3},
		{value: int32(4), want: 4},
		{value: int64(5), want: 5},
	} {
		got, ok := integerValue(test.value)
		if !ok || got != test.want {
			t.Errorf("integerValue(%T) = %d, %v; want %d, true", test.value, got, ok, test.want)
		}
	}
	if got, ok := integerValue(uint64(3)); ok || got != 0 {
		t.Fatalf("unsupported integer type was accepted: %d, %v", got, ok)
	}
}

func TestFormatTimeOmitsZeroAndPreservesSubsecondPrecision(t *testing.T) {
	if got := formatTime(time.Time{}); got != "" {
		t.Fatalf("zero time should be omitted, got %q", got)
	}
	value := time.Date(2026, time.January, 2, 3, 4, 5, 123456789, time.FixedZone("UTC+2", 2*60*60))
	if got, want := formatTime(value), value.Format(time.RFC3339Nano); got != want {
		t.Fatalf("formatted time = %q, want %q", got, want)
	}
}

func TestDryRunParsingMatchesHelmOperationSemantics(t *testing.T) {
	for _, test := range []struct {
		value string
		want  action.DryRunStrategy
	}{
		{value: "", want: action.DryRunNone},
		{value: "client", want: action.DryRunClient},
		{value: "server", want: action.DryRunServer},
		{value: "unknown", want: action.DryRunNone},
	} {
		if got := parseDryRun(test.value); got != test.want {
			t.Errorf("parseDryRun(%q) = %v, want %v", test.value, got, test.want)
		}
	}
	for _, test := range []struct {
		value string
		want  action.DryRunStrategy
	}{
		{value: "", want: action.DryRunClient},
		{value: "none", want: action.DryRunClient},
		{value: "client", want: action.DryRunClient},
		{value: "server", want: action.DryRunServer},
		{value: "unknown", want: action.DryRunServer},
	} {
		if got := parseTemplateDryRun(test.value); got != test.want {
			t.Errorf("parseTemplateDryRun(%q) = %v, want %v", test.value, got, test.want)
		}
	}
}

func TestNewRegistryClientAcceptsValidTlsAndPlainHttpOptions(t *testing.T) {
	certificate, privateKey := createRegistryTestCertificate(t)
	root := t.TempDir()
	certificatePath := filepath.Join(root, "client.pem")
	keyPath := filepath.Join(root, "client.key")
	caPath := filepath.Join(root, "ca.pem")
	for path, content := range map[string][]byte{
		certificatePath: certificate,
		keyPath:         privateKey,
		caPath:          certificate,
	} {
		if err := os.WriteFile(path, content, 0o600); err != nil {
			t.Fatal(err)
		}
	}

	client, err := newRegistryClient(request{
		CertFile: certificatePath, KeyFile: keyPath, CaFile: caPath,
		InsecureSkipTlsVerify: true, PlainHttp: true,
	}, filepath.Join(root, "registry.json"))
	if err != nil {
		t.Fatalf("valid registry TLS options were rejected: %v", err)
	}
	if client == nil {
		t.Fatal("expected a configured registry client")
	}
}

func TestNewRegistryClientReportsMissingTlsFiles(t *testing.T) {
	root := t.TempDir()
	_, err := newRegistryClient(request{
		CertFile: filepath.Join(root, "missing.pem"),
		KeyFile:  filepath.Join(root, "missing.key"),
	}, filepath.Join(root, "registry.json"))
	if err == nil || !strings.Contains(err.Error(), "can't load registry client certificate") {
		t.Fatalf("expected client certificate loading error, got %v", err)
	}

	_, err = newRegistryClient(request{CaFile: filepath.Join(root, "missing-ca.pem")}, filepath.Join(root, "registry.json"))
	if err == nil || !strings.Contains(err.Error(), "can't read registry CA file") {
		t.Fatalf("expected CA read error, got %v", err)
	}
}

func createRegistryTestCertificate(t *testing.T) ([]byte, []byte) {
	t.Helper()
	privateKey, err := ecdsa.GenerateKey(elliptic.P256(), rand.Reader)
	if err != nil {
		t.Fatal(err)
	}
	template := &x509.Certificate{
		SerialNumber:          big.NewInt(1),
		Subject:               pkix.Name{CommonName: "Helm.NET test CA"},
		NotBefore:             time.Now().Add(-time.Hour),
		NotAfter:              time.Now().Add(time.Hour),
		IsCA:                  true,
		BasicConstraintsValid: true,
		KeyUsage:              x509.KeyUsageCertSign | x509.KeyUsageDigitalSignature,
	}
	der, err := x509.CreateCertificate(rand.Reader, template, template, &privateKey.PublicKey, privateKey)
	if err != nil {
		t.Fatal(err)
	}
	certificate := pem.EncodeToMemory(&pem.Block{Type: "CERTIFICATE", Bytes: der})
	keyDER, err := x509.MarshalPKCS8PrivateKey(privateKey)
	if err != nil {
		t.Fatal(err)
	}
	key := pem.EncodeToMemory(&pem.Block{Type: "PRIVATE KEY", Bytes: keyDER})
	return certificate, key
}
