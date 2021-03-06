﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace PdfiumEditor
{
    internal abstract class PdfFile : IDisposable
    {
        private static readonly PdfLibrary _library = new PdfLibrary();

        public IntPtr _document;
        private IntPtr _form;
        private bool _disposed;
        //private NativeMethods.FPDF_FORMFILLINFO _formCallbacks;
        private GCHandle _formCallbacksHandle;

        public static PdfFile Create(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");

            if (stream is MemoryStream)
                return new PdfMemoryStreamFile((MemoryStream)stream);
            if (stream is FileStream)
                return new PdfFileStreamFile((FileStream)stream);
            return new PdfBufferFile(StreamExtensions.ToByteArray(stream));
        }

        public bool RenderPDFPageToDC(int pageNumber, IntPtr dc, int dpiX, int dpiY, int boundsOriginX, int boundsOriginY, int boundsWidth, int boundsHeight, bool fitToBounds, bool stretchToBounds, bool keepAspectRation, bool centerInBounds, bool autoRotate, bool forPrinting)
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().Name);

            using (var pageData = new PageData(_document, _form, pageNumber))
            {
                NativeMethods.FPDF_RenderPage(dc, pageData.Page, boundsOriginX, boundsOriginY, boundsWidth, boundsHeight, 0, forPrinting ? NativeMethods.FPDF.PRINTING : 0);
            }

            return true;
        }


        

        /// <summary>
        /// Appends the passed PDF to this one, all pages are inserted in the page index passed (usually pass pagecount here to append to the end)
        /// </summary>
        /// <param name="src_doc">The PDF to append to this one.</param>
        public bool Append_PDF(IntPtr src_doc,int pageno){
            int retval = 0;
            unsafe
            {
                retval = NativeMethods.FPDF_ImportPages(_document, src_doc, null, pageno);
            }
            if (retval == 0) return false;
            return true;
        }

        /// <summary>
        /// Extract Text from the page and rectangle passed.
        /// This converts the coords from the top left origin you pass to the bottom left of PDF
        /// coords passed are in pts so 72 per inch or 28.3 per cm
        /// page numbers are 0 based, the first page of a pdf is 0
        /// </summary>
        /// <param name="pageno">The page to extract text from.</param>
        unsafe public String Extract_text(int pageno, double left, double top, double right, double bottom, bool mode2)
        {
            double pheight;
            String addtext = "";
            char[] stringbuffer;
            ushort* ptrToBuf = null;
            int ccount;
            IntPtr page;
            IntPtr tpage;
            page = NativeMethods.FPDF_LoadPage(_document, pageno);
            pheight = NativeMethods.FPDF_GetPageHeight(page);
            tpage = NativeMethods.FPDFText_LoadPage(page);
            //convert the origin
            top = pheight - top;
            bottom = pheight - bottom;
            //call the function
            ccount = NativeMethods.FPDFText_GetBoundedText(tpage, left, top, right, bottom, ptrToBuf, 0);
            if (ccount > 0)
            {
                stringbuffer = new char[ccount];
                fixed (char* newptrToBuf = &stringbuffer[0])
                {
                    if (mode2)
                    {
                        ccount = NativeMethods.FPDFText_GetBoundedText2(tpage, left, top, right, bottom, (ushort*)newptrToBuf, ccount, 30);
                    }
                    else
                    {
                        ccount = NativeMethods.FPDFText_GetBoundedText(tpage, left, top, right, bottom, (ushort*)newptrToBuf, ccount);
                    }
                    addtext = new String(newptrToBuf);
                }
            }
            NativeMethods.FPDFText_ClosePage(tpage);
            //ptrToBuf = &stringbuffer[0];
            return addtext;
        }

        public List<SizeF> GetPDFDocInfo()
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().Name);

            int pageCount = NativeMethods.FPDF_GetPageCount(_document);
            var result = new List<SizeF>(pageCount);

            for (int i = 0; i < pageCount; i++)
            {
                double height;
                double width;
                NativeMethods.FPDF_GetPageSizeByIndex(_document, i, out width, out height);

                result.Add(new SizeF((float)width, (float)height));
            }

            return result;
        }

        public abstract void Save(Stream stream);

        protected void LoadDocument(IntPtr document)
        {
            _document = document;

            NativeMethods.FPDF_GetDocPermissions(_document);

            //_formCallbacks = new NativeMethods.FPDF_FORMFILLINFO();
            //_formCallbacksHandle = GCHandle.Alloc(_formCallbacks);
            //_formCallbacks.version = 1;

            //_form = NativeMethods.FPDFDOC_InitFormFillEnviroument(_document, ref _formCallbacks);
            //NativeMethods.FPDF_SetFormFieldHighlightColor(_form, 0, 0xFFE4DD);
            //NativeMethods.FPDF_SetFormFieldHighlightAlpha(_form, 100);

            //NativeMethods.FORM_DoDocumentJSAction(_form);
            //NativeMethods.FORM_DoDocumentOpenAction(_form);
        }

        

        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                //if (_form != IntPtr.Zero)
                //{
                    //NativeMethods.FORM_DoDocumentAAction(_form, NativeMethods.FPDFDOC_AACTION.WC);
                    //NativeMethods.FPDFDOC_ExitFormFillEnviroument(_form);
                    //_form = IntPtr.Zero;
                //}

                if (_document != IntPtr.Zero)
                {
                    NativeMethods.FPDF_CloseDocument(_document);
                    _document = IntPtr.Zero;
                }

                if (_formCallbacksHandle.IsAllocated)
                    _formCallbacksHandle.Free();

                _disposed = true;
            }
        }

        private class PageData : IDisposable
        {
            private readonly IntPtr _form;
            private bool _disposed;

            public IntPtr Page { get; private set; }

            public IntPtr TextPage { get; private set; }

            public double Width { get; private set; }

            public double Height { get; private set; }

            public PageData(IntPtr document, IntPtr form, int pageNumber)
            {
                _form = form;

                Page = NativeMethods.FPDF_LoadPage(document, pageNumber);
                TextPage = NativeMethods.FPDFText_LoadPage(Page);
                //NativeMethods.FORM_OnAfterLoadPage(Page, form);
                //NativeMethods.FORM_DoPageAAction(Page, form, NativeMethods.FPDFPAGE_AACTION.OPEN);

                Width = NativeMethods.FPDF_GetPageWidth(Page);
                Height = NativeMethods.FPDF_GetPageHeight(Page);
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    //NativeMethods.FORM_DoPageAAction(Page, _form, NativeMethods.FPDFPAGE_AACTION.CLOSE);
                    //NativeMethods.FORM_OnBeforeClosePage(Page, _form);
                    NativeMethods.FPDFText_ClosePage(TextPage);
                    NativeMethods.FPDF_ClosePage(Page);

                    _disposed = true;
                }
            }
        }
    }
}
