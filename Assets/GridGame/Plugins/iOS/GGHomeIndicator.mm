// Home indicator auto-hide per screen (GG-T2121). The C# side is Assets/GridGame/App/Boot/HomeIndicator.cs, which
// asks for it hidden in battle and shown everywhere else. Unity's root view controller answers
// prefersHomeIndicatorAutoHidden from the player-wide PlayerSettings.iOS.hideHomeButton, so this file replaces that
// method on the root controller's class with one that returns the flag set here, then asks UIKit to re-read it.
#import <UIKit/UIKit.h>
#import <objc/runtime.h>
#include "UnityInterface.h"

static BOOL gGGHomeIndicatorHidden = NO;
static NSMutableSet* gGGHomeIndicatorPatched = nil;

static BOOL GGHomeIndicator_Prefers(id self, SEL _cmd)
{
    return gGGHomeIndicatorHidden;
}

static void GGHomeIndicator_Patch(UIViewController* vc)
{
    Class cls = object_getClass(vc);
    if (gGGHomeIndicatorPatched == nil) gGGHomeIndicatorPatched = [NSMutableSet set];
    if ([gGGHomeIndicatorPatched containsObject:cls]) return;
    SEL sel = @selector(prefersHomeIndicatorAutoHidden);
    Method base = class_getInstanceMethod([UIViewController class], sel);
    if (base == NULL) return;
    class_replaceMethod(cls, sel, (IMP)GGHomeIndicator_Prefers, method_getTypeEncoding(base));
    [gGGHomeIndicatorPatched addObject:cls];
}

static void GGHomeIndicator_Apply(void)
{
    UIViewController* vc = UnityGetGLViewController();
    if (vc == nil) return;
    GGHomeIndicator_Patch(vc);
    if (@available(iOS 11.0, *)) [vc setNeedsUpdateOfHomeIndicatorAutoHidden];
}

extern "C" void GGHomeIndicator_SetAutoHidden(int hidden)
{
    gGGHomeIndicatorHidden = hidden != 0;
    if ([NSThread isMainThread]) GGHomeIndicator_Apply();
    else dispatch_async(dispatch_get_main_queue(), ^{ GGHomeIndicator_Apply(); });
}
