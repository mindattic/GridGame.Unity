// iCloud key-value store bridge for cloud save (BIBLE §15.2, GG-T609). The C# side is
// Assets/GridGame/App/Services/ICloudCloudBackend.cs; every decision (keys, value format, conflicts) is made there and in
// Meta/CloudSave. This file only moves strings in and out of NSUbiquitousKeyValueStore and forwards
// NSUbiquitousKeyValueStoreDidChangeExternallyNotification to Unity as "reason|key<TAB>key...".
// Needs the iCloud key-value storage entitlement (com.apple.developer.ubiquity-kvstore-identifier), which
// Assets/GridGame/Editor/ICloudEntitlements.cs adds to the exported Xcode project. Without it, or without an iCloud
// account on the device, GGICloud_Available() is false and the game treats the cloud as absent.
#import <Foundation/Foundation.h>

extern "C" void UnitySendMessage(const char* obj, const char* method, const char* msg);

static NSString* gListenerObject = nil;
static NSString* gListenerMethod = nil;
static BOOL gStoreOk = NO;
static id gObserver = nil;

static char* GGICloudCopy(NSString* s)
{
    if (s == nil) return NULL;
    const char* utf8 = [s UTF8String];
    if (utf8 == NULL) return NULL;
    // Mono frees a returned string with free(), so it must come from malloc.
    size_t n = strlen(utf8) + 1;
    char* out = (char*)malloc(n);
    if (out != NULL) memcpy(out, utf8, n);
    return out;
}

static BOOL GGICloudHasAccount(void)
{
    return [[NSFileManager defaultManager] ubiquityIdentityToken] != nil;
}

extern "C" {

void GGICloud_Start(const char* listenerObject, const char* listenerMethod)
{
    @autoreleasepool
    {
        gListenerObject = listenerObject ? [NSString stringWithUTF8String:listenerObject] : nil;
        gListenerMethod = listenerMethod ? [NSString stringWithUTF8String:listenerMethod] : nil;
        NSUbiquitousKeyValueStore* store = [NSUbiquitousKeyValueStore defaultStore];
        if (gObserver == nil)
        {
            gObserver = [[NSNotificationCenter defaultCenter]
                addObserverForName:NSUbiquitousKeyValueStoreDidChangeExternallyNotification
                            object:store
                             queue:[NSOperationQueue mainQueue]
                        usingBlock:^(NSNotification* note) {
                    if (gListenerObject == nil || gListenerMethod == nil) return;
                    NSNumber* reason = note.userInfo[NSUbiquitousKeyValueStoreChangeReasonKey];
                    NSArray* keys = note.userInfo[NSUbiquitousKeyValueStoreChangedKeysKey];
                    NSString* joined = [keys isKindOfClass:[NSArray class]] ? [keys componentsJoinedByString:@"\t"] : @"";
                    NSString* msg = [NSString stringWithFormat:@"%ld|%@", (long)(reason ? reason.integerValue : -1), joined];
                    UnitySendMessage([gListenerObject UTF8String], [gListenerMethod UTF8String], [msg UTF8String]);
                }];
        }
        // NO when the app lacks the key-value entitlement (or iCloud is otherwise unusable).
        gStoreOk = [store synchronize];
    }
}

bool GGICloud_Available()
{
    return gStoreOk && GGICloudHasAccount();
}

bool GGICloud_Synchronize()
{
    @autoreleasepool
    {
        gStoreOk = [[NSUbiquitousKeyValueStore defaultStore] synchronize];
        return gStoreOk;
    }
}

char* GGICloud_Get(const char* key)
{
    if (key == NULL) return NULL;
    @autoreleasepool
    {
        id value = [[NSUbiquitousKeyValueStore defaultStore] objectForKey:[NSString stringWithUTF8String:key]];
        return [value isKindOfClass:[NSString class]] ? GGICloudCopy((NSString*)value) : NULL;
    }
}

bool GGICloud_Set(const char* key, const char* value)
{
    if (key == NULL || value == NULL || !GGICloud_Available()) return false;
    @autoreleasepool
    {
        NSString* k = [NSString stringWithUTF8String:key];
        NSString* v = [NSString stringWithUTF8String:value];
        if (k == nil || v == nil) return false;
        NSUbiquitousKeyValueStore* store = [NSUbiquitousKeyValueStore defaultStore];
        [store setString:v forKey:k];
        [store synchronize];
        return true;
    }
}

}
