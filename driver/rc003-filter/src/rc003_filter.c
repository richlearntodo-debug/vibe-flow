#include "rc003_filter.h"

C_ASSERT(
    sizeof(RC003_FILTER_INFO) == 40 &&
    sizeof(RC003_FILTER_POLICY) == 272 &&
    sizeof(RC003_FILTER_EVENT) == 32 &&
    FIELD_OFFSET(RC003_FILTER_EVENT_BATCH, Events) == 24);

typedef struct _RC003_FILTER_GLOBAL_STATE {
    WDFSPINLOCK Lock;
    RC003_FILTER_POLICY Policy;
    ULONGLONG LastHeartbeat100ns;
    RC003_FILTER_EVENT Events[RC003_FILTER_EVENT_CAPACITY];
    ULONG EventHead;
    ULONG EventTail;
    ULONG EventCount;
    ULONGLONG LastSequence;
    ULONGLONG DroppedEventCount;
    volatile LONG AttachedDeviceCount;
} RC003_FILTER_GLOBAL_STATE;

static RC003_FILTER_GLOBAL_STATE g_State;
static NTSTATUS Rc003CreateControlDevice(_In_ WDFDRIVER Driver);
static BOOLEAN Rc003PolicyIsFreshLocked(_In_ ULONGLONG Now100ns);
static VOID Rc003DisarmLocked(VOID);
static VOID Rc003ClearEventsLocked(VOID);
static VOID Rc003EnqueueEventLocked(
    _In_ const KEYBOARD_INPUT_DATA* Input,
    _In_ ULONGLONG Now100ns
    );

NTSTATUS
DriverEntry(
    _In_ PDRIVER_OBJECT DriverObject,
    _In_ PUNICODE_STRING RegistryPath
    )
{
    WDF_DRIVER_CONFIG config;
    WDF_OBJECT_ATTRIBUTES lockAttributes;
    WDFDRIVER driver;
    NTSTATUS status;

    RtlZeroMemory(&g_State, sizeof(g_State));
    WDF_DRIVER_CONFIG_INIT(&config, Rc003EvtDeviceAdd);

    status = WdfDriverCreate(
        DriverObject,
        RegistryPath,
        WDF_NO_OBJECT_ATTRIBUTES,
        &config,
        &driver);
    if (!NT_SUCCESS(status)) {
        return status;
    }

    WDF_OBJECT_ATTRIBUTES_INIT(&lockAttributes);
    lockAttributes.ParentObject = driver;
    status = WdfSpinLockCreate(&lockAttributes, &g_State.Lock);
    if (!NT_SUCCESS(status)) {
        return status;
    }

    return Rc003CreateControlDevice(driver);
}

static NTSTATUS
Rc003CreateControlDevice(
    _In_ WDFDRIVER Driver
    )
{
    DECLARE_CONST_UNICODE_STRING(
        sddl,
        L"D:P(A;;GA;;;SY)(A;;GA;;;BA)(A;;GRGW;;;IU)");
    DECLARE_CONST_UNICODE_STRING(deviceName, L"\\Device\\VibeFlowRc003Filter");
    DECLARE_CONST_UNICODE_STRING(symbolicLink, L"\\DosDevices\\Global\\VibeFlowRc003Filter");
    PWDFDEVICE_INIT deviceInit;
    WDF_FILEOBJECT_CONFIG fileConfig;
    WDF_IO_QUEUE_CONFIG queueConfig;
    WDFDEVICE device;
    NTSTATUS status;

    deviceInit = WdfControlDeviceInitAllocate(Driver, &sddl);
    if (deviceInit == NULL) {
        return STATUS_INSUFFICIENT_RESOURCES;
    }

    WdfDeviceInitSetDeviceType(deviceInit, RC003_FILTER_DEVICE_TYPE);
    WdfDeviceInitSetExclusive(deviceInit, TRUE);
    status = WdfDeviceInitAssignName(deviceInit, &deviceName);
    if (!NT_SUCCESS(status)) {
        WdfDeviceInitFree(deviceInit);
        return status;
    }

    WDF_FILEOBJECT_CONFIG_INIT(
        &fileConfig,
        WDF_NO_EVENT_CALLBACK,
        Rc003EvtFileClose,
        WDF_NO_EVENT_CALLBACK);
    WdfDeviceInitSetFileObjectConfig(
        deviceInit,
        &fileConfig,
        WDF_NO_OBJECT_ATTRIBUTES);

    status = WdfDeviceCreate(
        &deviceInit,
        WDF_NO_OBJECT_ATTRIBUTES,
        &device);
    if (!NT_SUCCESS(status)) {
        if (deviceInit != NULL) {
            WdfDeviceInitFree(deviceInit);
        }
        return status;
    }

    status = WdfDeviceCreateSymbolicLink(device, &symbolicLink);
    if (!NT_SUCCESS(status)) {
        WdfObjectDelete(device);
        return status;
    }

    WDF_IO_QUEUE_CONFIG_INIT_DEFAULT_QUEUE(
        &queueConfig,
        WdfIoQueueDispatchParallel);
    queueConfig.EvtIoDeviceControl = Rc003EvtControlDeviceIoControl;
    status = WdfIoQueueCreate(
        device,
        &queueConfig,
        WDF_NO_OBJECT_ATTRIBUTES,
        WDF_NO_HANDLE);
    if (!NT_SUCCESS(status)) {
        WdfObjectDelete(device);
        return status;
    }

    WdfControlFinishInitializing(device);
    return STATUS_SUCCESS;
}

NTSTATUS
Rc003EvtDeviceAdd(
    _In_ WDFDRIVER Driver,
    _Inout_ PWDFDEVICE_INIT DeviceInit
    )
{
    WDF_OBJECT_ATTRIBUTES deviceAttributes;
    WDF_IO_QUEUE_CONFIG queueConfig;
    WDFDEVICE device;
    PRC003_FILTER_DEVICE_CONTEXT context;
    NTSTATUS status;

    UNREFERENCED_PARAMETER(Driver);

    WdfFdoInitSetFilter(DeviceInit);
    WdfDeviceInitSetDeviceType(DeviceInit, FILE_DEVICE_KEYBOARD);

    WDF_OBJECT_ATTRIBUTES_INIT_CONTEXT_TYPE(
        &deviceAttributes,
        RC003_FILTER_DEVICE_CONTEXT);
    deviceAttributes.EvtCleanupCallback = Rc003EvtDeviceCleanup;

    status = WdfDeviceCreate(
        &DeviceInit,
        &deviceAttributes,
        &device);
    if (!NT_SUCCESS(status)) {
        return status;
    }

    context = Rc003GetDeviceContext(device);
    RtlZeroMemory(context, sizeof(*context));

    WDF_IO_QUEUE_CONFIG_INIT_DEFAULT_QUEUE(
        &queueConfig,
        WdfIoQueueDispatchParallel);
    queueConfig.EvtIoInternalDeviceControl = Rc003EvtInternalDeviceControl;
    status = WdfIoQueueCreate(
        device,
        &queueConfig,
        WDF_NO_OBJECT_ATTRIBUTES,
        WDF_NO_HANDLE);
    if (!NT_SUCCESS(status)) {
        return status;
    }

    InterlockedIncrement(&g_State.AttachedDeviceCount);
    InterlockedExchange(&context->CountedAttached, 1);
    return STATUS_SUCCESS;
}

VOID
Rc003EvtDeviceCleanup(
    _In_ WDFOBJECT DeviceObject
    )
{
    WDFDEVICE device;
    PRC003_FILTER_DEVICE_CONTEXT context;

    device = (WDFDEVICE)DeviceObject;
    context = Rc003GetDeviceContext(device);
    if (InterlockedExchange(&context->CountedAttached, 0) == 0) {
        return;
    }

    if (InterlockedDecrement(&g_State.AttachedDeviceCount) <= 0) {
        InterlockedExchange(&g_State.AttachedDeviceCount, 0);
        WdfSpinLockAcquire(g_State.Lock);
        Rc003DisarmLocked();
        WdfSpinLockRelease(g_State.Lock);
    }
}

VOID
Rc003EvtInternalDeviceControl(
    _In_ WDFQUEUE Queue,
    _In_ WDFREQUEST Request,
    _In_ size_t OutputBufferLength,
    _In_ size_t InputBufferLength,
    _In_ ULONG IoControlCode
    )
{
    WDFDEVICE device;
    PRC003_FILTER_DEVICE_CONTEXT context;
    PCONNECT_DATA connectData;
    WDF_REQUEST_SEND_OPTIONS options;
    size_t length;
    NTSTATUS status;

    UNREFERENCED_PARAMETER(OutputBufferLength);
    UNREFERENCED_PARAMETER(InputBufferLength);

    device = WdfIoQueueGetDevice(Queue);
    context = Rc003GetDeviceContext(device);
    status = STATUS_SUCCESS;

    if (IoControlCode == IOCTL_INTERNAL_KEYBOARD_CONNECT) {
        if (context->UpperConnectData.ClassService != NULL) {
            status = STATUS_SHARING_VIOLATION;
        }
        else {
            status = WdfRequestRetrieveInputBuffer(
                Request,
                sizeof(CONNECT_DATA),
                (PVOID*)&connectData,
                &length);
            if (NT_SUCCESS(status)) {
                context->UpperConnectData = *connectData;
                connectData->ClassDeviceObject = WdfDeviceWdmGetDeviceObject(device);
#pragma warning(push)
#pragma warning(disable: 4152)
                connectData->ClassService = Rc003KeyboardServiceCallback;
#pragma warning(pop)
            }
        }
    }
    else if (IoControlCode == IOCTL_INTERNAL_KEYBOARD_DISCONNECT) {
        // Keyboard class stacks do not support a live disconnect/reconnect of
        // this callback. PnP removal tears down the device context instead.
        status = STATUS_NOT_IMPLEMENTED;
    }

    if (!NT_SUCCESS(status)) {
        WdfRequestComplete(Request, status);
        return;
    }

    WDF_REQUEST_SEND_OPTIONS_INIT(
        &options,
        WDF_REQUEST_SEND_OPTION_SEND_AND_FORGET);
    if (!WdfRequestSend(
        Request,
        WdfDeviceGetIoTarget(device),
        &options)) {
        status = WdfRequestGetStatus(Request);
        WdfRequestComplete(Request, status);
    }
}

VOID
Rc003KeyboardServiceCallback(
    _In_ PDEVICE_OBJECT DeviceObject,
    _In_ PKEYBOARD_INPUT_DATA InputDataStart,
    _In_ PKEYBOARD_INPUT_DATA InputDataEnd,
    _Inout_ PULONG InputDataConsumed
    )
{
    WDFDEVICE device;
    PRC003_FILTER_DEVICE_CONTEXT context;
    PKEYBOARD_INPUT_DATA source;
    PKEYBOARD_INPUT_DATA destination;
    ULONGLONG now100ns;
    ULONG originalCount;
    ULONG droppedCount;
    ULONG forwardedConsumed;
    BOOLEAN policyFresh;

    device = WdfWdmDeviceGetWdfDeviceHandle(DeviceObject);
    context = Rc003GetDeviceContext(device);
    originalCount = (ULONG)(InputDataEnd - InputDataStart);
    droppedCount = 0;
    destination = InputDataStart;
    now100ns = KeQueryInterruptTime();

    WdfSpinLockAcquire(g_State.Lock);
    policyFresh = Rc003PolicyIsFreshLocked(now100ns);
    for (source = InputDataStart; source < InputDataEnd; ++source) {
        BOOLEAN suppress;

        Rc003EnqueueEventLocked(source, now100ns);
        suppress = policyFresh &&
            (ULONG)source->MakeCode < RC003_FILTER_SCAN_CODE_COUNT &&
            g_State.Policy.SuppressScanCode[source->MakeCode] != 0;
        if (suppress) {
            ++droppedCount;
            continue;
        }
        if (destination != source) {
            *destination = *source;
        }
        ++destination;
    }
    WdfSpinLockRelease(g_State.Lock);

    if (destination == InputDataStart) {
        *InputDataConsumed = originalCount;
        return;
    }

    if (context->UpperConnectData.ClassService == NULL) {
        *InputDataConsumed = originalCount;
        return;
    }

    forwardedConsumed = 0;
    ((PSERVICE_CALLBACK_ROUTINE)(ULONG_PTR)context->UpperConnectData.ClassService)(
        context->UpperConnectData.ClassDeviceObject,
        InputDataStart,
        destination,
        &forwardedConsumed);
    *InputDataConsumed = forwardedConsumed + droppedCount;
}

static BOOLEAN
Rc003PolicyIsFreshLocked(
    _In_ ULONGLONG Now100ns
    )
{
    ULONGLONG age;

    if (g_State.Policy.Enabled == 0 || g_State.LastHeartbeat100ns == 0) {
        return FALSE;
    }
    if (Now100ns < g_State.LastHeartbeat100ns) {
        Rc003DisarmLocked();
        return FALSE;
    }
    age = Now100ns - g_State.LastHeartbeat100ns;
    if (age > RC003_FILTER_HEARTBEAT_TIMEOUT_100NS) {
        Rc003DisarmLocked();
        return FALSE;
    }
    return TRUE;
}

static VOID
Rc003DisarmLocked(VOID)
{
    g_State.Policy.Enabled = 0;
    g_State.LastHeartbeat100ns = 0;
    RtlZeroMemory(
        g_State.Policy.SuppressScanCode,
        sizeof(g_State.Policy.SuppressScanCode));
}

static VOID
Rc003ClearEventsLocked(VOID)
{
    g_State.EventHead = 0;
    g_State.EventTail = 0;
    g_State.EventCount = 0;
}

static VOID
Rc003EnqueueEventLocked(
    _In_ const KEYBOARD_INPUT_DATA* Input,
    _In_ ULONGLONG Now100ns
    )
{
    PRC003_FILTER_EVENT target;

    if (g_State.EventCount == RC003_FILTER_EVENT_CAPACITY) {
        g_State.EventTail =
            (g_State.EventTail + 1U) % RC003_FILTER_EVENT_CAPACITY;
        --g_State.EventCount;
        ++g_State.DroppedEventCount;
    }

    target = &g_State.Events[g_State.EventHead];
    RtlZeroMemory(target, sizeof(*target));
    target->Magic = RC003_FILTER_MAGIC;
    target->StructureSize = (UINT32)sizeof(*target);
    target->Sequence = ++g_State.LastSequence;
    target->InterruptTime100ns = Now100ns;
    target->MakeCode = Input->MakeCode;
    target->Flags = Input->Flags;
    target->UnitId = Input->UnitId;

    g_State.EventHead =
        (g_State.EventHead + 1U) % RC003_FILTER_EVENT_CAPACITY;
    ++g_State.EventCount;
}

VOID
Rc003EvtControlDeviceIoControl(
    _In_ WDFQUEUE Queue,
    _In_ WDFREQUEST Request,
    _In_ size_t OutputBufferLength,
    _In_ size_t InputBufferLength,
    _In_ ULONG IoControlCode
    )
{
    NTSTATUS status;
    size_t bytesTransferred;
    ULONGLONG now100ns;

    UNREFERENCED_PARAMETER(Queue);
    status = STATUS_SUCCESS;
    bytesTransferred = 0;
    now100ns = KeQueryInterruptTime();

    switch (IoControlCode) {
    case IOCTL_RC003_FILTER_GET_INFO:
    {
        PRC003_FILTER_INFO info;
        size_t bufferLength;

        status = WdfRequestRetrieveOutputBuffer(
            Request,
            sizeof(*info),
            (PVOID*)&info,
            &bufferLength);
        if (!NT_SUCCESS(status)) {
            break;
        }

        WdfSpinLockAcquire(g_State.Lock);
        RtlZeroMemory(info, sizeof(*info));
        info->Magic = RC003_FILTER_MAGIC;
        info->ApiVersion = RC003_FILTER_API_VERSION;
        info->StructureSize = (UINT32)sizeof(*info);
        info->AttachedDeviceCount = (UINT32)g_State.AttachedDeviceCount;
        info->PolicyArmed = Rc003PolicyIsFreshLocked(now100ns) ? 1U : 0U;
        info->QueueDepth = g_State.EventCount;
        info->LastSequence = g_State.LastSequence;
        info->DroppedEventCount = g_State.DroppedEventCount;
        WdfSpinLockRelease(g_State.Lock);
        bytesTransferred = sizeof(*info);
        break;
    }

    case IOCTL_RC003_FILTER_SET_POLICY:
    {
        PRC003_FILTER_POLICY policy;
        size_t bufferLength;
        ULONG index;

        status = WdfRequestRetrieveInputBuffer(
            Request,
            sizeof(*policy),
            (PVOID*)&policy,
            &bufferLength);
        if (!NT_SUCCESS(status)) {
            break;
        }
        if (policy->Magic != RC003_FILTER_MAGIC ||
            policy->ApiVersion != RC003_FILTER_API_VERSION ||
            policy->StructureSize != (UINT32)sizeof(*policy)) {
            status = STATUS_REVISION_MISMATCH;
            break;
        }

        WdfSpinLockAcquire(g_State.Lock);
        if (g_State.AttachedDeviceCount <= 0) {
            status = STATUS_DEVICE_NOT_READY;
            WdfSpinLockRelease(g_State.Lock);
            break;
        }
        // Establish a clean session boundary before the new policy can
        // suppress input or expose events to its user-mode owner.
        Rc003ClearEventsLocked();
        g_State.Policy = *policy;
        g_State.Policy.Enabled = policy->Enabled != 0 ? 1U : 0U;
        for (index = 0; index < RC003_FILTER_SCAN_CODE_COUNT; ++index) {
            g_State.Policy.SuppressScanCode[index] =
                policy->SuppressScanCode[index] != 0 ? 1U : 0U;
        }
        g_State.LastHeartbeat100ns =
            g_State.Policy.Enabled != 0 ? now100ns : 0;
        WdfSpinLockRelease(g_State.Lock);
        break;
    }

    case IOCTL_RC003_FILTER_HEARTBEAT:
        WdfSpinLockAcquire(g_State.Lock);
        if (g_State.Policy.Enabled == 0) {
            status = STATUS_DEVICE_NOT_READY;
        }
        else {
            g_State.LastHeartbeat100ns = now100ns;
        }
        WdfSpinLockRelease(g_State.Lock);
        break;

    case IOCTL_RC003_FILTER_READ_EVENTS:
    {
        PRC003_FILTER_EVENT_BATCH batch;
        size_t bufferLength;
        size_t headerSize;
        ULONG capacity;
        ULONG count;

        headerSize = FIELD_OFFSET(RC003_FILTER_EVENT_BATCH, Events);
        status = WdfRequestRetrieveOutputBuffer(
            Request,
            headerSize,
            (PVOID*)&batch,
            &bufferLength);
        if (!NT_SUCCESS(status)) {
            break;
        }
        capacity = (ULONG)((bufferLength - headerSize) /
            sizeof(RC003_FILTER_EVENT));
        if (capacity == 0) {
            status = STATUS_BUFFER_TOO_SMALL;
            break;
        }

        WdfSpinLockAcquire(g_State.Lock);
        count = 0;
        while (count < capacity && g_State.EventCount > 0) {
            batch->Events[count] = g_State.Events[g_State.EventTail];
            g_State.EventTail =
                (g_State.EventTail + 1U) % RC003_FILTER_EVENT_CAPACITY;
            --g_State.EventCount;
            ++count;
        }
        batch->Magic = RC003_FILTER_MAGIC;
        batch->ApiVersion = RC003_FILTER_API_VERSION;
        batch->StructureSize = (UINT32)headerSize;
        batch->EventCount = count;
        batch->DroppedEventCount = g_State.DroppedEventCount;
        WdfSpinLockRelease(g_State.Lock);

        bytesTransferred = headerSize +
            ((size_t)count * sizeof(RC003_FILTER_EVENT));
        break;
    }

    case IOCTL_RC003_FILTER_DISARM:
        WdfSpinLockAcquire(g_State.Lock);
        Rc003DisarmLocked();
        WdfSpinLockRelease(g_State.Lock);
        break;

    default:
        status = STATUS_INVALID_DEVICE_REQUEST;
        break;
    }

    WdfRequestCompleteWithInformation(
        Request,
        status,
        NT_SUCCESS(status) ? bytesTransferred : 0);

    UNREFERENCED_PARAMETER(OutputBufferLength);
    UNREFERENCED_PARAMETER(InputBufferLength);
}

VOID
Rc003EvtFileClose(
    _In_ WDFFILEOBJECT FileObject
    )
{
    UNREFERENCED_PARAMETER(FileObject);

    WdfSpinLockAcquire(g_State.Lock);
    Rc003DisarmLocked();
    WdfSpinLockRelease(g_State.Lock);
}
