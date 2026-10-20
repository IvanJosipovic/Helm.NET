package main

/*
#include <stdlib.h>
*/
import "C"

import "unsafe"

//export HelmExecuteJSON
func HelmExecuteJSON(input *C.char) *C.char {
	return C.CString(string(handleJSON([]byte(C.GoString(input)))))
}

//export HelmFree
func HelmFree(value unsafe.Pointer) {
	C.free(value)
}
