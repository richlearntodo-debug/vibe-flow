#pragma once

#pragma warning(push)
#pragma warning(disable: 4201)
#include <ntddk.h>
#include <kbdmou.h>
#include <ntddkbd.h>
#include <wdf.h>
#pragma warning(pop)

#include "public.h"

typedef struct _RC003_FILTER_DEVICE_CONTEXT {
    CONNECT_DATA UpperConnectData;
    volatile LONG CountedAttached;
} RC003_FILTER_DEVICE_CONTEXT, *PRC003_FILTER_DEVICE_CONTEXT;

WDF_DECLARE_CONTEXT_TYPE_WITH_NAME(RC003_FILTER_DEVICE_CONTEXT, Rc003GetDeviceContext)

DRIVER_INITIALIZE DriverEntry;
EVT_WDF_DRIVER_DEVICE_ADD Rc003EvtDeviceAdd;
EVT_WDF_IO_QUEUE_IO_INTERNAL_DEVICE_CONTROL Rc003EvtInternalDeviceControl;
EVT_WDF_IO_QUEUE_IO_DEVICE_CONTROL Rc003EvtControlDeviceIoControl;
EVT_WDF_FILE_CLOSE Rc003EvtFileClose;
EVT_WDF_OBJECT_CONTEXT_CLEANUP Rc003EvtDeviceCleanup;

VOID
Rc003KeyboardServiceCallback(
    _In_ PDEVICE_OBJECT DeviceObject,
    _In_ PKEYBOARD_INPUT_DATA InputDataStart,
    _In_ PKEYBOARD_INPUT_DATA InputDataEnd,
    _Inout_ PULONG InputDataConsumed
    );
