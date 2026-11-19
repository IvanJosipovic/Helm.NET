//go:build linux

package main

/*
#define _GNU_SOURCE
#include <signal.h>

// CoreCLR registers its GC activation handler for SIGRTMIN without
// SA_ONSTACK. Go's runtime marks an existing SIGRTMIN handler with
// SA_ONSTACK during initialization, which forces that handler onto the
// host's small alternate signal stack and can overflow it under GC
// suspension. Restore the host's original disposition before any managed
// call can observe the change.
static void helmshim_restore_activation_signal(void) {
	struct sigaction current;
	if (sigaction(SIGRTMIN, NULL, &current) != 0) {
		return;
	}
	current.sa_flags &= ~SA_ONSTACK;
	sigaction(SIGRTMIN, &current, NULL);
}

static int helmshim_activation_signal_uses_altstack(void) {
	struct sigaction current;
	if (sigaction(SIGRTMIN, NULL, &current) != 0) {
		return -1;
	}
	return (current.sa_flags & SA_ONSTACK) != 0;
}
*/
import "C"

func init() {
	C.helmshim_restore_activation_signal()
}

func activationSignalUsesAltstack() int {
	return int(C.helmshim_activation_signal_uses_altstack())
}
