package com.bigsudoku.app;

import android.app.Activity;
import android.graphics.Color;
import android.os.Build;
import android.os.Bundle;
import android.view.View;
import android.view.WindowInsets;
import android.webkit.WebResourceRequest;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.FrameLayout;

// Shows the web version of the game (bundled inside the app) full-screen.
public class MainActivity extends Activity {
    private static final int PAPER = Color.rgb(250, 247, 238);

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        FrameLayout root = new FrameLayout(this);
        root.setBackgroundColor(PAPER);

        WebView web = new WebView(this);
        web.setBackgroundColor(PAPER);
        WebSettings s = web.getSettings();
        s.setJavaScriptEnabled(true);
        s.setDomStorageEnabled(true); // saves the game between launches
        s.setTextZoom(100);           // the game sizes its own text to fit the screen
        s.setSupportZoom(false);
        web.setWebViewClient(new WebViewClient() {
            @Override
            public boolean shouldOverrideUrlLoading(WebView view, WebResourceRequest request) {
                return !"file".equals(request.getUrl().getScheme()); // never leave the game
            }
        });
        root.addView(web, new FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.MATCH_PARENT, FrameLayout.LayoutParams.MATCH_PARENT));

        // Keep the game clear of the status bar, navigation bar and camera cut-out.
        root.setOnApplyWindowInsetsListener((v, insets) -> {
            if (Build.VERSION.SDK_INT >= 30) {
                android.graphics.Insets i = insets.getInsets(
                        WindowInsets.Type.systemBars() | WindowInsets.Type.displayCutout());
                v.setPadding(i.left, i.top, i.right, i.bottom);
            } else {
                v.setPadding(insets.getSystemWindowInsetLeft(), insets.getSystemWindowInsetTop(),
                        insets.getSystemWindowInsetRight(), insets.getSystemWindowInsetBottom());
            }
            return insets;
        });

        setContentView(root);
        if (Build.VERSION.SDK_INT >= 30) {
            android.view.WindowInsetsController c = getWindow().getInsetsController();
            if (c != null) {
                int light = android.view.WindowInsetsController.APPEARANCE_LIGHT_STATUS_BARS
                        | android.view.WindowInsetsController.APPEARANCE_LIGHT_NAVIGATION_BARS;
                c.setSystemBarsAppearance(light, light);
            }
        } else {
            root.setSystemUiVisibility(View.SYSTEM_UI_FLAG_LIGHT_STATUS_BAR);
        }

        web.loadUrl("file:///android_asset/www/index.html?app=android");
    }
}
