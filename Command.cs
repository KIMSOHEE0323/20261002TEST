using System;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Modless
{
    /// <summary>
    /// 외부 명령: 모드리스 폼을 띄웁니다.
    /// Add-In Manager 에서 bin 폴더의 Modless.dll 을 불러와 실행합니다.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand
    {
        // 폼이 여러 개 열리지 않도록 하나만 보관합니다.
        private static MainForm _form;
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // 이미 열려 있으면 앞으로 가져오기만 합니다.
            if (_form != null && !_form.IsDisposed)
            {
                // 최소화되어 있으면 원래 크기로 되돌립니다.
                if (_form.WindowState == System.Windows.Forms.FormWindowState.Minimized)
                    _form.WindowState = System.Windows.Forms.FormWindowState.Normal;

                _form.Activate();
                return Result.Succeeded;
            }

            // 폼은 반드시 여기(Revit API 컨텍스트 안)에서 만들어야 합니다.
            // 폼 생성자에서 ExternalEvent.Create() 를 호출하기 때문입니다.
            _form = new MainForm();

            // Show() : 모드리스 (Revit 을 계속 조작할 수 있음)
            // ShowDialog() : 모달 (폼을 닫을 때까지 Revit 조작 불가)
            _form.Show(new RevitWindow(commandData.Application.MainWindowHandle));

            return Result.Succeeded;
        }

        /// <summary>
        /// Revit 메인 창을 폼의 소유자(Owner)로 지정하기 위한 래퍼.
        /// 폼이 Revit 창 뒤로 숨지 않고, Revit 을 최소화하면 함께 최소화됩니다.
        /// </summary>
        private class RevitWindow : IWin32Window
        {
            public RevitWindow(IntPtr handle) { Handle = handle; }
            public IntPtr Handle { get; }
        }
    }
}
